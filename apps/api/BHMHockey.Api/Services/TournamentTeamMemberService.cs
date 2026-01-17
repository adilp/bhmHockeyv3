using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for managing tournament team members with invitation workflow (TRN-011).
/// Supports organizer-driven team building with Pending/Accepted/Declined statuses.
/// </summary>
public class TournamentTeamMemberService : ITournamentTeamMemberService
{
    private readonly AppDbContext _context;
    private readonly ITournamentService _tournamentService;
    private readonly INotificationService _notificationService;

    public TournamentTeamMemberService(
        AppDbContext context,
        ITournamentService tournamentService,
        INotificationService notificationService)
    {
        _context = context;
        _tournamentService = tournamentService;
        _notificationService = notificationService;
    }

    public async Task<TournamentTeamMemberDto> AddPlayerAsync(
        Guid tournamentId,
        Guid teamId,
        Guid userId,
        Guid adminUserId)
    {
        // 1. Check admin permission
        var isAdmin = await _tournamentService.CanUserManageTournamentAsync(tournamentId, adminUserId);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to add players to this tournament");
        }

        // 2. Validate tournament exists and get it (need MaxPlayersPerTeam)
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
        {
            throw new InvalidOperationException("Tournament not found");
        }

        // 3. Validate team exists and belongs to tournament
        var team = await _context.TournamentTeams.FindAsync(teamId);
        if (team == null || team.TournamentId != tournamentId)
        {
            throw new InvalidOperationException("Team not found in this tournament");
        }

        // 4. Validate user exists
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // 5. Check team isn't full: count members with Status="Accepted" AND LeftAt=null against tournament.MaxPlayersPerTeam
        if (tournament.MaxPlayersPerTeam.HasValue)
        {
            var currentAcceptedCount = await _context.TournamentTeamMembers
                .CountAsync(m => m.TeamId == teamId &&
                                 m.Status == "Accepted" &&
                                 m.LeftAt == null);

            if (currentAcceptedCount >= tournament.MaxPlayersPerTeam.Value)
            {
                throw new InvalidOperationException(
                    $"Team is full (MaxPlayersPerTeam: {tournament.MaxPlayersPerTeam.Value})");
            }
        }

        // 6. Check player isn't on another team in same tournament
        // Look for any TournamentTeamMember in a team belonging to this tournament
        // where Status != "Declined" and LeftAt = null
        var existingMembership = await _context.TournamentTeamMembers
            .Include(m => m.Team)
            .FirstOrDefaultAsync(m =>
                m.UserId == userId &&
                m.Team.TournamentId == tournamentId &&
                m.Status != "Declined" &&
                m.LeftAt == null);

        if (existingMembership != null)
        {
            throw new InvalidOperationException(
                $"Player is already on another team in this tournament (Status: {existingMembership.Status})");
        }

        // 7. Create TournamentTeamMember with Status="Pending", JoinedAt=now
        var member = new TournamentTeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            UserId = userId,
            Role = "Player",
            Status = "Pending",
            JoinedAt = DateTime.UtcNow
        };

        _context.TournamentTeamMembers.Add(member);
        await _context.SaveChangesAsync();

        // Reload to get User navigation property
        await _context.Entry(member).Reference(m => m.User).LoadAsync();

        // 8. Send push notification to player (use user's PushToken if available)
        if (!string.IsNullOrEmpty(user.PushToken))
        {
            try
            {
                await _notificationService.SendPushNotificationAsync(
                    user.PushToken,
                    "Team Invitation",
                    $"You've been invited to join {team.Name} in {tournament.Name}",
                    new { tournamentId, teamId },
                    userId,
                    "tournament_team_invite",
                    null,
                    null);
            }
            catch
            {
                // Don't fail the add operation if notification fails
                // Notification service will log the error
            }
        }

        // 9. Return mapped DTO
        return MapToDto(member);
    }

    public async Task<bool> RemovePlayerAsync(
        Guid tournamentId,
        Guid teamId,
        Guid userId,
        Guid adminUserId)
    {
        // 1. Check admin permission
        var isAdmin = await _tournamentService.CanUserManageTournamentAsync(tournamentId, adminUserId);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to remove players from this tournament");
        }

        // 2. Find the member (by TeamId and UserId, LeftAt = null)
        var member = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(m =>
                m.TeamId == teamId &&
                m.UserId == userId &&
                m.LeftAt == null);

        if (member == null)
        {
            return false;
        }

        // 3. Set LeftAt = DateTime.UtcNow (soft delete)
        member.LeftAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<TournamentTeamMemberDto> RespondAsync(
        Guid tournamentId,
        Guid teamId,
        Guid userId,
        bool accept,
        string? position,
        string? customResponses)
    {
        // 1. Find the member (by TeamId and UserId, LeftAt = null)
        var member = await _context.TournamentTeamMembers
            .Include(m => m.User)
            .Include(m => m.Team)
            .FirstOrDefaultAsync(m =>
                m.TeamId == teamId &&
                m.UserId == userId &&
                m.LeftAt == null);

        if (member == null)
        {
            throw new InvalidOperationException("Team member invitation not found");
        }

        // 2. Check Status == "Pending" (can't respond twice)
        if (member.Status != "Pending")
        {
            throw new InvalidOperationException(
                $"You have already responded to this invitation (Status: {member.Status})");
        }

        // 3. If accept
        if (accept)
        {
            // Validate position is provided
            if (string.IsNullOrEmpty(position))
            {
                throw new InvalidOperationException("Position is required when accepting invitation");
            }

            // Validate position value
            if (position != "Goalie" && position != "Skater")
            {
                throw new InvalidOperationException("Position must be 'Goalie' or 'Skater'");
            }

            // Set Status = "Accepted", RespondedAt = now, Position = position
            member.Status = "Accepted";
            member.RespondedAt = DateTime.UtcNow;
            member.Position = position;

            // Create TournamentRegistration with Status="Assigned", Position=position,
            // CustomResponses=customResponses, AssignedTeamId=teamId
            var registration = new TournamentRegistration
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                UserId = userId,
                Status = "Assigned",
                Position = position,
                AssignedTeamId = teamId,
                CustomResponses = customResponses,
                RegisteredAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.TournamentRegistrations.Add(registration);
        }
        else
        {
            // 4. If decline: Set Status = "Declined", RespondedAt = now
            member.Status = "Declined";
            member.RespondedAt = DateTime.UtcNow;
        }

        // 5. Save and return DTO
        await _context.SaveChangesAsync();

        return MapToDto(member);
    }

    public async Task<List<TournamentTeamMemberDto>> GetTeamMembersAsync(
        Guid tournamentId,
        Guid teamId)
    {
        // 1. Query members where TeamId matches and LeftAt = null
        var members = await _context.TournamentTeamMembers
            .Include(m => m.User)
            .Where(m => m.TeamId == teamId && m.LeftAt == null)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync();

        // 2. Map to DTOs and return
        return members.Select(MapToDto).ToList();
    }

    #region Helper Methods

    /// <summary>
    /// Maps a TournamentTeamMember entity to TournamentTeamMemberDto.
    /// Includes user information from the User navigation property.
    /// </summary>
    private TournamentTeamMemberDto MapToDto(TournamentTeamMember member)
    {
        return new TournamentTeamMemberDto
        {
            Id = member.Id,
            TeamId = member.TeamId,
            UserId = member.UserId,
            UserFirstName = member.User.FirstName,
            UserLastName = member.User.LastName,
            UserEmail = member.User.Email,
            Role = member.Role,
            Status = member.Status,
            Position = member.Position,
            JoinedAt = member.JoinedAt,
            RespondedAt = member.RespondedAt,
            LeftAt = member.LeftAt
        };
    }

    #endregion
}
