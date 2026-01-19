using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class TournamentService : ITournamentService
{
    private readonly AppDbContext _context;
    private readonly IOrganizationAdminService _orgAdminService;
    private readonly ITournamentAuthorizationService _authService;

    // Valid enum values
    private static readonly HashSet<string> ValidFormats = new()
    {
        "SingleElimination", "DoubleElimination", "RoundRobin"
    };

    private static readonly HashSet<string> ValidTeamFormations = new()
    {
        "OrganizerAssigned", "PreFormed"
    };

    private static readonly HashSet<string> ValidStatuses = new()
    {
        "Draft", "Open", "RegistrationClosed", "InProgress", "Completed", "Postponed", "Cancelled"
    };

    // Public statuses visible in list endpoint
    private static readonly HashSet<string> PublicStatuses = new()
    {
        "Open", "InProgress", "Completed"
    };

    // Statuses that allow updates
    private static readonly HashSet<string> EditableStatuses = new()
    {
        "Draft", "Open"
    };

    public TournamentService(AppDbContext context, IOrganizationAdminService orgAdminService, ITournamentAuthorizationService authService)
    {
        _context = context;
        _orgAdminService = orgAdminService;
        _authService = authService;
    }

    public async Task<TournamentDto> CreateAsync(CreateTournamentRequest request, Guid creatorId)
    {
        // Validate format
        if (!ValidFormats.Contains(request.Format))
        {
            throw new InvalidOperationException(
                $"Invalid format '{request.Format}'. Valid values: {string.Join(", ", ValidFormats)}");
        }

        // Validate team formation
        if (!ValidTeamFormations.Contains(request.TeamFormation))
        {
            throw new InvalidOperationException(
                $"Invalid team formation '{request.TeamFormation}'. Valid values: {string.Join(", ", ValidTeamFormations)}");
        }

        // If organization is specified, validate it exists and user is an admin
        if (request.OrganizationId.HasValue)
        {
            var org = await _context.Organizations.FindAsync(request.OrganizationId.Value);
            if (org == null)
            {
                throw new InvalidOperationException("Organization not found");
            }

            var isOrgAdmin = await _orgAdminService.IsUserAdminAsync(request.OrganizationId.Value, creatorId);
            if (!isOrgAdmin)
            {
                throw new UnauthorizedAccessException("You must be an organization admin to create tournaments for this organization");
            }
        }

        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            CreatorId = creatorId,
            Name = request.Name,
            Description = request.Description,
            Format = request.Format,
            TeamFormation = request.TeamFormation,
            Status = "Draft",
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RegistrationDeadline = request.RegistrationDeadline,
            MaxTeams = request.MaxTeams,
            MinPlayersPerTeam = request.MinPlayersPerTeam,
            MaxPlayersPerTeam = request.MaxPlayersPerTeam,
            AllowMultiTeam = request.AllowMultiTeam,
            AllowSubstitutions = request.AllowSubstitutions,
            EntryFee = request.EntryFee,
            FeeType = request.FeeType,
            PointsWin = request.PointsWin,
            PointsTie = request.PointsTie,
            PointsLoss = request.PointsLoss,
            PlayoffFormat = request.PlayoffFormat,
            PlayoffTeamsCount = request.PlayoffTeamsCount,
            RulesContent = request.RulesContent,
            WaiverUrl = request.WaiverUrl,
            Venue = request.Venue,
            NotificationSettings = request.NotificationSettings,
            CustomQuestions = request.CustomQuestions,
            EligibilityRequirements = request.EligibilityRequirements,
            TiebreakerOrder = request.TiebreakerOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tournaments.Add(tournament);

        // Create Owner admin entry for the creator
        var adminEntry = new TournamentAdmin
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            UserId = creatorId,
            Role = "Owner",
            AddedAt = DateTime.UtcNow,
            AddedByUserId = null
        };

        _context.TournamentAdmins.Add(adminEntry);

        await _context.SaveChangesAsync();

        return await MapToDto(tournament, creatorId);
    }

    public async Task<List<TournamentDto>> GetAllAsync(Guid? currentUserId = null)
    {
        var tournaments = await _context.Tournaments
            .Include(t => t.Organization)
            .Where(t => PublicStatuses.Contains(t.Status))
            .OrderByDescending(t => t.StartDate)
            .ToListAsync();

        var dtos = new List<TournamentDto>();
        foreach (var tournament in tournaments)
        {
            dtos.Add(await MapToDto(tournament, currentUserId));
        }

        return dtos;
    }

    public async Task<TournamentDto?> GetByIdAsync(Guid id, Guid? currentUserId = null)
    {
        var tournament = await _context.Tournaments
            .Include(t => t.Organization)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tournament == null)
        {
            return null;
        }

        return await MapToDto(tournament, currentUserId);
    }

    public async Task<TournamentDto?> UpdateAsync(Guid id, UpdateTournamentRequest request, Guid userId)
    {
        var tournament = await _context.Tournaments
            .Include(t => t.Organization)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tournament == null)
        {
            return null;
        }

        // Check authorization (Admin or Owner can update)
        var canManage = await _authService.IsAdminAsync(id, userId);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("You are not authorized to update this tournament");
        }

        // Check if tournament is in an editable status
        if (!EditableStatuses.Contains(tournament.Status))
        {
            throw new InvalidOperationException(
                $"Cannot update tournament in '{tournament.Status}' status. Updates are only allowed in: {string.Join(", ", EditableStatuses)}");
        }

        // Validate format if provided
        if (request.Format != null && !ValidFormats.Contains(request.Format))
        {
            throw new InvalidOperationException(
                $"Invalid format '{request.Format}'. Valid values: {string.Join(", ", ValidFormats)}");
        }

        // Validate team formation if provided
        if (request.TeamFormation != null && !ValidTeamFormations.Contains(request.TeamFormation))
        {
            throw new InvalidOperationException(
                $"Invalid team formation '{request.TeamFormation}'. Valid values: {string.Join(", ", ValidTeamFormations)}");
        }

        // Apply updates (patch semantics - only update provided fields)
        if (request.Name != null) tournament.Name = request.Name;
        if (request.Description != null) tournament.Description = request.Description;
        if (request.Format != null) tournament.Format = request.Format;
        if (request.TeamFormation != null) tournament.TeamFormation = request.TeamFormation;
        if (request.StartDate.HasValue) tournament.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue) tournament.EndDate = request.EndDate.Value;
        if (request.RegistrationDeadline.HasValue) tournament.RegistrationDeadline = request.RegistrationDeadline.Value;
        if (request.MaxTeams.HasValue) tournament.MaxTeams = request.MaxTeams.Value;
        if (request.MinPlayersPerTeam.HasValue) tournament.MinPlayersPerTeam = request.MinPlayersPerTeam.Value;
        if (request.MaxPlayersPerTeam.HasValue) tournament.MaxPlayersPerTeam = request.MaxPlayersPerTeam.Value;
        if (request.AllowMultiTeam.HasValue) tournament.AllowMultiTeam = request.AllowMultiTeam.Value;
        if (request.AllowSubstitutions.HasValue) tournament.AllowSubstitutions = request.AllowSubstitutions.Value;
        if (request.EntryFee.HasValue) tournament.EntryFee = request.EntryFee.Value;
        if (request.FeeType != null) tournament.FeeType = request.FeeType;
        if (request.PointsWin.HasValue) tournament.PointsWin = request.PointsWin.Value;
        if (request.PointsTie.HasValue) tournament.PointsTie = request.PointsTie.Value;
        if (request.PointsLoss.HasValue) tournament.PointsLoss = request.PointsLoss.Value;
        if (request.PlayoffFormat != null) tournament.PlayoffFormat = request.PlayoffFormat;
        if (request.PlayoffTeamsCount.HasValue) tournament.PlayoffTeamsCount = request.PlayoffTeamsCount.Value;
        if (request.RulesContent != null) tournament.RulesContent = request.RulesContent;
        if (request.WaiverUrl != null) tournament.WaiverUrl = request.WaiverUrl;
        if (request.Venue != null) tournament.Venue = request.Venue;
        if (request.NotificationSettings != null) tournament.NotificationSettings = request.NotificationSettings;
        if (request.CustomQuestions != null) tournament.CustomQuestions = request.CustomQuestions;
        if (request.EligibilityRequirements != null) tournament.EligibilityRequirements = request.EligibilityRequirements;
        if (request.TiebreakerOrder != null) tournament.TiebreakerOrder = request.TiebreakerOrder;

        tournament.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await MapToDto(tournament, userId);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        var tournament = await _context.Tournaments.FindAsync(id);

        if (tournament == null)
        {
            return false;
        }

        // Check authorization (Owner only can delete)
        var canDelete = await _authService.CanDeleteTournamentAsync(id, userId);
        if (!canDelete)
        {
            throw new UnauthorizedAccessException("You are not authorized to delete this tournament");
        }

        // Only allow delete in Draft status
        if (tournament.Status != "Draft")
        {
            throw new InvalidOperationException(
                $"Cannot delete tournament in '{tournament.Status}' status. Deletion is only allowed for tournaments in 'Draft' status.");
        }

        // Delete tournament admins first (cascade should handle this, but be explicit)
        var admins = await _context.TournamentAdmins
            .Where(ta => ta.TournamentId == id)
            .ToListAsync();
        _context.TournamentAdmins.RemoveRange(admins);

        // Delete the tournament
        _context.Tournaments.Remove(tournament);

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> CanUserManageTournamentAsync(Guid tournamentId, Guid userId)
    {
        // Delegate to authorization service - any role (Scorekeeper+) can manage
        return await _authService.IsScorekeeperAsync(tournamentId, userId);
    }

    public async Task<MyTournamentsResponseDto> GetUserTournamentsAsync(Guid userId, Guid? currentUserId, UserTournamentsFilterDto? filter)
    {
        var isOwnProfile = currentUserId.HasValue && currentUserId.Value == userId;

        // Query user's tournaments from three sources in parallel:
        // 1. TournamentTeamMember - player on a team
        // 2. TournamentRegistration - registered but maybe not assigned to team
        // 3. TournamentAdmin - organizer/admin

        // Run all three queries in parallel for better performance
        var teamMemberTask = _context.TournamentTeamMembers
            .Include(ttm => ttm.Team)
                .ThenInclude(t => t.Tournament)
                    .ThenInclude(tournament => tournament.Organization)
            .Where(ttm => ttm.UserId == userId)
            .Select(ttm => new
            {
                Tournament = ttm.Team.Tournament,
                TeamId = (Guid?)ttm.Team.Id,
                TeamName = ttm.Team.Name,
                FinalPlacement = ttm.Team.FinalPlacement,
                TeamMemberRole = ttm.Role,
                AdminRole = (string?)null
            })
            .ToListAsync();

        var registrationTask = _context.TournamentRegistrations
            .Include(tr => tr.Tournament)
                .ThenInclude(t => t.Organization)
            .Include(tr => tr.AssignedTeam)
            .Where(tr => tr.UserId == userId && tr.Status != "Cancelled")
            .Select(tr => new
            {
                Tournament = tr.Tournament,
                TeamId = tr.AssignedTeamId,
                TeamName = tr.AssignedTeam != null ? tr.AssignedTeam.Name : null,
                FinalPlacement = tr.AssignedTeam != null ? tr.AssignedTeam.FinalPlacement : null,
                TeamMemberRole = (string?)null,
                AdminRole = (string?)null
            })
            .ToListAsync();

        var adminTask = _context.TournamentAdmins
            .Include(ta => ta.Tournament)
                .ThenInclude(t => t.Organization)
            .Where(ta => ta.UserId == userId && ta.RemovedAt == null)
            .Select(ta => new
            {
                Tournament = ta.Tournament,
                TeamId = (Guid?)null,
                TeamName = (string?)null,
                FinalPlacement = (int?)null,
                TeamMemberRole = (string?)null,
                AdminRole = ta.Role
            })
            .ToListAsync();

        // Wait for all queries to complete
        await Task.WhenAll(teamMemberTask, registrationTask, adminTask);

        var teamMemberTournaments = teamMemberTask.Result;
        var registrationTournaments = registrationTask.Result;
        var adminTournaments = adminTask.Result;

        // Combine all sources
        var allTournaments = teamMemberTournaments
            .Concat(registrationTournaments)
            .Concat(adminTournaments)
            .ToList();

        // Group by tournament to handle duplicates (user could be both player and admin)
        var tournamentGroups = allTournaments
            .GroupBy(t => t.Tournament.Id)
            .Select(g =>
            {
                // If user is both admin and player, admin role takes priority
                var hasAdminRole = g.Any(x => x.AdminRole != null);
                var adminItem = g.FirstOrDefault(x => x.AdminRole != null);
                var teamItem = g.FirstOrDefault(x => x.TeamId != null);

                return new
                {
                    Tournament = g.First().Tournament,
                    TeamId = teamItem?.TeamId,
                    TeamName = teamItem?.TeamName,
                    FinalPlacement = teamItem?.FinalPlacement,
                    TeamMemberRole = teamItem?.TeamMemberRole,
                    AdminRole = adminItem?.AdminRole,
                    HasAdminRole = hasAdminRole
                };
            })
            .ToList();

        // Apply exclusions
        tournamentGroups = tournamentGroups
            .Where(t =>
            {
                // Exclude Draft tournaments unless user is admin
                if (t.Tournament.Status == "Draft" && !t.HasAdminRole)
                    return false;

                // Exclude Cancelled tournaments
                if (t.Tournament.Status == "Cancelled")
                    return false;

                // Exclude Postponed tournaments
                if (t.Tournament.Status == "Postponed")
                    return false;

                return true;
            })
            .ToList();

        // Apply filters
        if (filter != null)
        {
            // Filter by won
            if (filter.Filter == "won")
            {
                tournamentGroups = tournamentGroups
                    .Where(t => t.FinalPlacement.HasValue && t.FinalPlacement.Value == 1)
                    .ToList();
            }

            // Filter by year
            if (filter.Year.HasValue)
            {
                tournamentGroups = tournamentGroups
                    .Where(t => t.Tournament.StartDate.Year == filter.Year.Value)
                    .ToList();
            }
        }

        // Categorize tournaments
        var active = new List<UserTournamentSummaryDto>();
        var past = new List<UserTournamentSummaryDto>();
        var organizing = new List<UserTournamentSummaryDto>();

        foreach (var item in tournamentGroups)
        {
            var dto = new UserTournamentSummaryDto
            {
                Id = item.Tournament.Id,
                Name = item.Tournament.Name,
                Status = item.Tournament.Status,
                StartDate = item.Tournament.StartDate,
                EndDate = item.Tournament.EndDate,
                CompletedAt = item.Tournament.CompletedAt,
                TeamId = item.TeamId?.ToString(),
                TeamName = item.TeamName,
                FinalPlacement = item.FinalPlacement,
                UserRole = DetermineUserRole(item.AdminRole, item.TeamMemberRole),
                OrganizationId = item.Tournament.OrganizationId?.ToString(),
                OrganizationName = item.Tournament.Organization?.Name
            };

            // Categorization logic
            if (item.HasAdminRole)
            {
                // Admins go to Organizing section (any status except Cancelled)
                organizing.Add(dto);
            }
            else if (item.Tournament.Status == "Open" || item.Tournament.Status == "InProgress")
            {
                // Active tournaments (player only)
                active.Add(dto);
            }
            else if (item.Tournament.Status == "Completed")
            {
                // Past tournaments (player)
                past.Add(dto);
            }
        }

        // Apply ordering
        active = active.OrderBy(t => t.StartDate).ToList();
        past = past.OrderByDescending(t => t.EndDate).ToList();
        organizing = organizing.OrderByDescending(t => t.StartDate).ToList();

        // Privacy: if viewing another user's profile, only return Past section
        if (!isOwnProfile)
        {
            return new MyTournamentsResponseDto
            {
                Active = new List<UserTournamentSummaryDto>(),
                Past = past,
                Organizing = new List<UserTournamentSummaryDto>()
            };
        }

        return new MyTournamentsResponseDto
        {
            Active = active,
            Past = past,
            Organizing = organizing
        };
    }

    public async Task<List<UpcomingTournamentMatchDto>> GetUpcomingMatchesForUserAsync(Guid userId)
    {
        // Find all teams the user is a member of (with Accepted status)
        var userTeamIds = await _context.TournamentTeamMembers
            .Where(ttm => ttm.UserId == userId && ttm.Status == "Accepted")
            .Select(ttm => ttm.TeamId)
            .ToListAsync();

        if (!userTeamIds.Any())
        {
            return new List<UpcomingTournamentMatchDto>();
        }

        // Find all upcoming matches for these teams
        var matches = await _context.TournamentMatches
            .Include(m => m.Tournament)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m =>
                (m.HomeTeamId.HasValue && userTeamIds.Contains(m.HomeTeamId.Value) ||
                 m.AwayTeamId.HasValue && userTeamIds.Contains(m.AwayTeamId.Value)) &&
                (m.Status == "Scheduled" || m.Status == "InProgress"))
            .OrderBy(m => m.ScheduledTime.HasValue ? m.ScheduledTime.Value : DateTime.MaxValue)
            .ToListAsync();

        var result = new List<UpcomingTournamentMatchDto>();

        foreach (var match in matches)
        {
            // Determine if user's team is home or away
            var isHomeTeam = match.HomeTeamId.HasValue && userTeamIds.Contains(match.HomeTeamId.Value);
            var userTeamId = isHomeTeam ? match.HomeTeamId!.Value : match.AwayTeamId!.Value;
            var userTeamName = isHomeTeam ? match.HomeTeam!.Name : match.AwayTeam!.Name;

            // Get opponent team info (may be null if TBD)
            Guid? opponentTeamId = null;
            string? opponentTeamName = null;

            if (isHomeTeam && match.AwayTeamId.HasValue)
            {
                opponentTeamId = match.AwayTeamId.Value;
                opponentTeamName = match.AwayTeam?.Name;
            }
            else if (!isHomeTeam && match.HomeTeamId.HasValue)
            {
                opponentTeamId = match.HomeTeamId.Value;
                opponentTeamName = match.HomeTeam?.Name;
            }

            result.Add(new UpcomingTournamentMatchDto
            {
                Id = match.Id,
                TournamentId = match.TournamentId,
                TournamentName = match.Tournament.Name,
                UserTeamId = userTeamId,
                UserTeamName = userTeamName,
                OpponentTeamId = opponentTeamId,
                OpponentTeamName = opponentTeamName,
                Round = match.Round,
                MatchNumber = match.MatchNumber,
                BracketPosition = match.BracketPosition,
                Status = match.Status,
                ScheduledTime = match.ScheduledTime,
                Venue = match.Venue,
                IsHomeTeam = isHomeTeam
            });
        }

        return result;
    }

    private string DetermineUserRole(string? adminRole, string? teamMemberRole)
    {
        // Admin roles take priority: Owner > Admin > Scorekeeper > Captain > Player
        if (adminRole != null)
            return adminRole;

        if (teamMemberRole != null)
            return teamMemberRole;

        return "Player";
    }

    private async Task<TournamentDto> MapToDto(Tournament tournament, Guid? currentUserId)
    {
        var canManage = currentUserId.HasValue
            ? await CanUserManageTournamentAsync(tournament.Id, currentUserId.Value)
            : false;

        return new TournamentDto
        {
            Id = tournament.Id,
            OrganizationId = tournament.OrganizationId,
            OrganizationName = tournament.Organization?.Name,
            CreatorId = tournament.CreatorId,
            Name = tournament.Name,
            Description = tournament.Description,
            Format = tournament.Format,
            TeamFormation = tournament.TeamFormation,
            Status = tournament.Status,
            StartDate = tournament.StartDate,
            EndDate = tournament.EndDate,
            RegistrationDeadline = tournament.RegistrationDeadline,
            PostponedToDate = tournament.PostponedToDate,
            MaxTeams = tournament.MaxTeams,
            MinPlayersPerTeam = tournament.MinPlayersPerTeam,
            MaxPlayersPerTeam = tournament.MaxPlayersPerTeam,
            AllowMultiTeam = tournament.AllowMultiTeam,
            AllowSubstitutions = tournament.AllowSubstitutions,
            EntryFee = tournament.EntryFee,
            FeeType = tournament.FeeType,
            PointsWin = tournament.PointsWin,
            PointsTie = tournament.PointsTie,
            PointsLoss = tournament.PointsLoss,
            PlayoffFormat = tournament.PlayoffFormat,
            PlayoffTeamsCount = tournament.PlayoffTeamsCount,
            RulesContent = tournament.RulesContent,
            WaiverUrl = tournament.WaiverUrl,
            Venue = tournament.Venue,
            NotificationSettings = tournament.NotificationSettings,
            CustomQuestions = tournament.CustomQuestions,
            EligibilityRequirements = tournament.EligibilityRequirements,
            TiebreakerOrder = tournament.TiebreakerOrder,
            CreatedAt = tournament.CreatedAt,
            UpdatedAt = tournament.UpdatedAt,
            PublishedAt = tournament.PublishedAt,
            StartedAt = tournament.StartedAt,
            CompletedAt = tournament.CompletedAt,
            CancelledAt = tournament.CancelledAt,
            CanManage = canManage
        };
    }
}
