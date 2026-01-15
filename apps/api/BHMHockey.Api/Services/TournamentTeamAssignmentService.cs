using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for managing team assignments in tournaments.
/// Handles manual assignment, auto-assignment, and bulk team creation operations.
/// </summary>
public class TournamentTeamAssignmentService : ITournamentTeamAssignmentService
{
    private readonly AppDbContext _context;
    private readonly ITournamentService _tournamentService;

    // Valid statuses for team assignment operations (before tournament starts)
    private static readonly HashSet<string> ValidAssignmentStatuses = new()
    {
        "Draft", "Open", "RegistrationClosed"
    };

    public TournamentTeamAssignmentService(
        AppDbContext context,
        ITournamentService tournamentService)
    {
        _context = context;
        _tournamentService = tournamentService;
    }

    public async Task<TournamentRegistrationDto?> AssignPlayerToTeamAsync(
        Guid tournamentId,
        Guid registrationId,
        Guid teamId,
        Guid adminUserId)
    {
        // 1. Verify user is admin
        var isAdmin = await _tournamentService.CanUserManageTournamentAsync(tournamentId, adminUserId);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to assign players for this tournament");
        }

        // 2. Get tournament and verify status
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
        {
            throw new InvalidOperationException("Tournament not found");
        }

        if (!ValidAssignmentStatuses.Contains(tournament.Status))
        {
            throw new InvalidOperationException(
                $"Cannot assign players when tournament is in '{tournament.Status}' status. " +
                $"Team assignment is only allowed in: {string.Join(", ", ValidAssignmentStatuses)}");
        }

        // 3. Find registration by ID, verify it belongs to tournament
        var registration = await _context.TournamentRegistrations
            .Include(r => r.User)
            .Include(r => r.AssignedTeam)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.TournamentId == tournamentId);

        if (registration == null)
        {
            return null;
        }

        // 4. Find team by ID, verify it belongs to tournament
        var team = await _context.TournamentTeams.FindAsync(teamId);
        if (team == null || team.TournamentId != tournamentId)
        {
            throw new InvalidOperationException("Team not found in this tournament");
        }

        // 5. If player already assigned to different team, remove old TournamentTeamMember
        if (registration.AssignedTeamId.HasValue && registration.AssignedTeamId != teamId)
        {
            var oldMember = await _context.TournamentTeamMembers
                .FirstOrDefaultAsync(tm => tm.TeamId == registration.AssignedTeamId && tm.UserId == registration.UserId);
            if (oldMember != null)
            {
                _context.TournamentTeamMembers.Remove(oldMember);
            }
        }

        // 6. Create new TournamentTeamMember with Role="Player"
        var newMember = new TournamentTeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            UserId = registration.UserId,
            Role = "Player",
            JoinedAt = DateTime.UtcNow
        };
        _context.TournamentTeamMembers.Add(newMember);

        // 7. Update registration: AssignedTeamId = teamId, Status = "Assigned"
        registration.AssignedTeamId = teamId;
        registration.Status = "Assigned";
        registration.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Reload to get updated team name
        await _context.Entry(registration).Reference(r => r.AssignedTeam).LoadAsync();

        return MapToDto(registration);
    }

    public async Task<TeamAssignmentResultDto> AutoAssignTeamsAsync(
        Guid tournamentId,
        bool balanceBySkillLevel,
        Guid adminUserId)
    {
        // 1. Verify user is admin
        var isAdmin = await _tournamentService.CanUserManageTournamentAsync(tournamentId, adminUserId);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to auto-assign players for this tournament");
        }

        // 2. Get tournament and verify status
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
        {
            throw new InvalidOperationException("Tournament not found");
        }

        if (!ValidAssignmentStatuses.Contains(tournament.Status))
        {
            throw new InvalidOperationException(
                $"Cannot assign players when tournament is in '{tournament.Status}' status. " +
                $"Team assignment is only allowed in: {string.Join(", ", ValidAssignmentStatuses)}");
        }

        // 3. Get all teams for tournament
        var teams = await _context.TournamentTeams
            .Where(t => t.TournamentId == tournamentId)
            .OrderBy(t => t.Name)
            .ToListAsync();

        if (teams.Count == 0)
        {
            throw new InvalidOperationException("No teams exist for this tournament. Create teams first.");
        }

        // 4. Get all unassigned registrations (Status != "Assigned" and AssignedTeamId == null)
        var unassignedRegistrations = await _context.TournamentRegistrations
            .Include(r => r.User)
            .Where(r => r.TournamentId == tournamentId &&
                       r.Status != "Assigned" &&
                       r.AssignedTeamId == null &&
                       r.Status != "Cancelled")
            .ToListAsync();

        if (unassignedRegistrations.Count == 0)
        {
            return new TeamAssignmentResultDto
            {
                Success = true,
                Message = "No unassigned players to assign",
                AssignedCount = 0,
                UnassignedCount = 0
            };
        }

        // 5. Separate registrations into goalies and skaters
        var goalies = unassignedRegistrations
            .Where(r => r.Position?.Equals("Goalie", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var skaters = unassignedRegistrations
            .Where(r => !r.Position?.Equals("Goalie", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        int assignedCount = 0;

        // 6. Distribute goalies round-robin (ensure each team gets at least 1 if possible)
        for (int i = 0; i < goalies.Count; i++)
        {
            var goalie = goalies[i];
            var team = teams[i % teams.Count];

            await AssignPlayerToTeamInternal(goalie, team);
            assignedCount++;
        }

        // 7. Distribute skaters based on balanceBySkillLevel
        if (balanceBySkillLevel)
        {
            // Sort by skill level: Gold > Silver > Bronze > D-League > null
            var sortedSkaters = skaters
                .OrderBy(r => GetSkillLevelRank(GetPlayerSkillLevel(r)))
                .ToList();

            // Snake draft: teams[0,1,2,3,3,2,1,0,0,1,2,3...]
            int teamIndex = 0;
            bool forward = true;

            for (int i = 0; i < sortedSkaters.Count; i++)
            {
                var skater = sortedSkaters[i];
                var team = teams[teamIndex];

                await AssignPlayerToTeamInternal(skater, team);
                assignedCount++;

                // Snake draft logic
                if (forward)
                {
                    teamIndex++;
                    if (teamIndex >= teams.Count)
                    {
                        teamIndex = teams.Count - 1;
                        forward = false;
                    }
                }
                else
                {
                    teamIndex--;
                    if (teamIndex < 0)
                    {
                        teamIndex = 0;
                        forward = true;
                    }
                }
            }
        }
        else
        {
            // Random distribution - shuffle then round-robin
            var random = new Random();
            var shuffledSkaters = skaters.OrderBy(x => random.Next()).ToList();

            for (int i = 0; i < shuffledSkaters.Count; i++)
            {
                var skater = shuffledSkaters[i];
                var team = teams[i % teams.Count];

                await AssignPlayerToTeamInternal(skater, team);
                assignedCount++;
            }
        }

        await _context.SaveChangesAsync();

        return new TeamAssignmentResultDto
        {
            Success = true,
            Message = $"Successfully assigned {assignedCount} players to {teams.Count} teams",
            AssignedCount = assignedCount,
            UnassignedCount = 0
        };
    }

    public async Task<BulkCreateTeamsResponse> BulkCreateTeamsAsync(
        Guid tournamentId,
        int count,
        string namePrefix,
        Guid adminUserId)
    {
        // 1. Verify user is admin
        var isAdmin = await _tournamentService.CanUserManageTournamentAsync(tournamentId, adminUserId);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to create teams for this tournament");
        }

        // 2. Get tournament and verify status
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
        {
            throw new InvalidOperationException("Tournament not found");
        }

        if (!ValidAssignmentStatuses.Contains(tournament.Status))
        {
            throw new InvalidOperationException(
                $"Cannot create teams when tournament is in '{tournament.Status}' status. " +
                $"Team creation is only allowed in: {string.Join(", ", ValidAssignmentStatuses)}");
        }

        // 3. Validate count
        if (count <= 0)
        {
            throw new InvalidOperationException("Count must be greater than 0");
        }

        // 4. Create teams with sequential names
        var createdTeams = new List<TournamentTeamDto>();

        for (int i = 1; i <= count; i++)
        {
            var team = new TournamentTeam
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                Name = $"{namePrefix} {i}",
                Status = "Registered",
                HasBye = false,
                Wins = 0,
                Losses = 0,
                Ties = 0,
                Points = 0,
                GoalsFor = 0,
                GoalsAgainst = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.TournamentTeams.Add(team);

            // Create DTO for response
            createdTeams.Add(new TournamentTeamDto
            {
                Id = team.Id,
                TournamentId = team.TournamentId,
                Name = team.Name,
                CaptainUserId = null,
                CaptainName = null,
                Status = team.Status,
                WaitlistPosition = null,
                Seed = null,
                FinalPlacement = null,
                HasBye = team.HasBye,
                Wins = team.Wins,
                Losses = team.Losses,
                Ties = team.Ties,
                Points = team.Points,
                GoalsFor = team.GoalsFor,
                GoalsAgainst = team.GoalsAgainst,
                GoalDifferential = 0,
                PaymentStatus = null,
                CreatedAt = team.CreatedAt,
                UpdatedAt = team.UpdatedAt
            });
        }

        await _context.SaveChangesAsync();

        return new BulkCreateTeamsResponse
        {
            Teams = createdTeams,
            Message = $"Successfully created {count} teams"
        };
    }

    public async Task<bool> RemovePlayerFromTeamAsync(
        Guid tournamentId,
        Guid registrationId,
        Guid adminUserId)
    {
        // 1. Verify user is admin
        var isAdmin = await _tournamentService.CanUserManageTournamentAsync(tournamentId, adminUserId);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to remove players for this tournament");
        }

        // 2. Get tournament and verify status
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
        {
            throw new InvalidOperationException("Tournament not found");
        }

        if (!ValidAssignmentStatuses.Contains(tournament.Status))
        {
            throw new InvalidOperationException(
                $"Cannot remove players when tournament is in '{tournament.Status}' status. " +
                $"Player removal is only allowed in: {string.Join(", ", ValidAssignmentStatuses)}");
        }

        // 3. Find registration
        var registration = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.TournamentId == tournamentId);

        if (registration == null)
        {
            return false;
        }

        // 4. Check if assigned
        if (!registration.AssignedTeamId.HasValue)
        {
            return false;
        }

        // 5. Delete TournamentTeamMember record (hard delete)
        var teamMember = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(tm => tm.TeamId == registration.AssignedTeamId && tm.UserId == registration.UserId);

        if (teamMember != null)
        {
            _context.TournamentTeamMembers.Remove(teamMember);
        }

        // 6. Clear registration's AssignedTeamId, set Status back to "Registered"
        registration.AssignedTeamId = null;
        registration.Status = "Registered";
        registration.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    #region Helper Methods

    /// <summary>
    /// Internal helper to assign a player to a team without saving changes.
    /// Used by auto-assignment to batch database updates.
    /// </summary>
    private async Task AssignPlayerToTeamInternal(TournamentRegistration registration, TournamentTeam team)
    {
        // Create team member record
        var teamMember = new TournamentTeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            UserId = registration.UserId,
            Role = "Player",
            JoinedAt = DateTime.UtcNow
        };
        _context.TournamentTeamMembers.Add(teamMember);

        // Update registration
        registration.AssignedTeamId = team.Id;
        registration.Status = "Assigned";
        registration.UpdatedAt = DateTime.UtcNow;

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the skill level for a player based on their position in the registration.
    /// Returns the skill level from User.Positions dictionary using the position key.
    /// </summary>
    private string? GetPlayerSkillLevel(TournamentRegistration registration)
    {
        if (registration.User?.Positions == null || string.IsNullOrEmpty(registration.Position))
        {
            return null;
        }

        // Position key is lowercase in the Positions dictionary
        var positionKey = registration.Position.ToLower();

        if (registration.User.Positions.TryGetValue(positionKey, out var skillLevel))
        {
            return skillLevel;
        }

        return null;
    }

    /// <summary>
    /// Returns int rank for sorting skill levels: Gold=1, Silver=2, Bronze=3, D-League=4, null=5.
    /// Lower rank = higher skill level.
    /// </summary>
    private int GetSkillLevelRank(string? skillLevel)
    {
        return skillLevel switch
        {
            "Gold" => 1,
            "Silver" => 2,
            "Bronze" => 3,
            "D-League" => 4,
            _ => 5 // null or unknown
        };
    }

    /// <summary>
    /// Maps a TournamentRegistration entity to TournamentRegistrationDto.
    /// Includes UserDto for the user and AssignedTeamName if AssignedTeamId is set.
    /// </summary>
    private TournamentRegistrationDto MapToDto(TournamentRegistration registration)
    {
        var user = registration.User;
        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.Positions,
            user.VenmoHandle,
            user.Role,
            user.CreatedAt,
            null,  // Badges not included in tournament registration responses
            0      // Total badge count
        );

        return new TournamentRegistrationDto(
            registration.Id,
            registration.TournamentId,
            userDto,
            registration.Status,
            registration.Position,
            registration.WaitlistPosition,
            registration.PromotedAt,
            registration.IsWaitlisted,
            registration.AssignedTeamId,
            registration.AssignedTeam?.Name,
            registration.CustomResponses,
            registration.WaiverStatus,
            registration.PaymentStatus,
            registration.PaymentMarkedAt,
            registration.PaymentVerifiedAt,
            registration.PaymentDeadlineAt,
            registration.RegisteredAt,
            registration.UpdatedAt,
            registration.CancelledAt
        );
    }

    #endregion
}
