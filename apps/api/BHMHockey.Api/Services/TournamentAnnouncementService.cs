using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for managing tournament announcements with role-based visibility filtering.
/// Admins see all announcements, captains see "All", "Captains", and team-specific,
/// regular players see "All" and team-specific, non-participants see only "All".
/// </summary>
public class TournamentAnnouncementService : ITournamentAnnouncementService
{
    private readonly AppDbContext _context;
    private readonly ITournamentAuthorizationService _authService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<TournamentAnnouncementService> _logger;

    public TournamentAnnouncementService(
        AppDbContext context,
        ITournamentAuthorizationService authService,
        INotificationService notificationService,
        ILogger<TournamentAnnouncementService> logger)
    {
        _context = context;
        _authService = authService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<List<TournamentAnnouncementDto>> GetAnnouncementsAsync(Guid tournamentId, Guid? requesterId)
    {
        // Fetch all non-deleted announcements
        var announcements = await _context.TournamentAnnouncements
            .Include(a => a.CreatedByUser)
            .Where(a => a.TournamentId == tournamentId && a.DeletedAt == null)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        // Determine user's role and permissions
        bool isAdmin = false;
        bool isCaptain = false;
        List<Guid> userTeamIds = new();

        if (requesterId.HasValue)
        {
            // Check if user is admin
            isAdmin = await _authService.IsAdminAsync(tournamentId, requesterId.Value);

            if (!isAdmin)
            {
                // Check if user is a captain of any team in tournament
                var captainTeamIds = await _context.TournamentTeams
                    .Where(t => t.TournamentId == tournamentId && t.CaptainUserId == requesterId.Value)
                    .Select(t => t.Id)
                    .ToListAsync();

                isCaptain = captainTeamIds.Any();

                // Get all teams the user is on (captain or member)
                userTeamIds = await _context.TournamentTeamMembers
                    .Where(m => m.UserId == requesterId.Value && m.Team.TournamentId == tournamentId)
                    .Select(m => m.TeamId)
                    .ToListAsync();

                // Add captain teams to user's teams
                userTeamIds.AddRange(captainTeamIds);
                userTeamIds = userTeamIds.Distinct().ToList();
            }
        }

        // Filter announcements based on visibility
        var filteredAnnouncements = announcements.Where(a =>
        {
            // Admins see everything
            if (isAdmin)
                return true;

            // Check target type
            if (a.Target == "All")
                return true;

            if (a.Target == "Admins")
                return false; // Non-admins cannot see admin-only announcements

            if (a.Target == "Captains")
                return isCaptain;

            // Team-specific announcements (TargetTeamIds is set)
            if (!string.IsNullOrEmpty(a.TargetTeamIds))
            {
                var targetTeamIds = JsonSerializer.Deserialize<List<Guid>>(a.TargetTeamIds) ?? new List<Guid>();
                return targetTeamIds.Any(teamId => userTeamIds.Contains(teamId));
            }

            return false;
        }).ToList();

        // Map to DTOs
        return filteredAnnouncements.Select(a => new TournamentAnnouncementDto(
            a.Id,
            a.TournamentId,
            a.Title,
            a.Body,
            a.Target,
            string.IsNullOrEmpty(a.TargetTeamIds) ? null : JsonSerializer.Deserialize<List<Guid>>(a.TargetTeamIds),
            a.CreatedByUserId,
            a.CreatedByUser.FirstName,
            a.CreatedByUser.LastName,
            a.CreatedAt,
            a.UpdatedAt
        )).ToList();
    }

    public async Task<TournamentAnnouncementDto> CreateAnnouncementAsync(Guid tournamentId, CreateTournamentAnnouncementRequest request, Guid requesterId)
    {
        // Check requester is admin
        var isAdmin = await _authService.IsAdminAsync(tournamentId, requesterId);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("Only tournament admins can create announcements.");
        }

        // Validate request
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("Title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new InvalidOperationException("Body is required.");
        }

        // Create announcement
        var announcement = new TournamentAnnouncement
        {
            TournamentId = tournamentId,
            Title = request.Title,
            Body = request.Body,
            Target = request.Target,
            TargetTeamIds = request.TargetTeamIds != null ? JsonSerializer.Serialize(request.TargetTeamIds) : null,
            CreatedByUserId = requesterId,
            CreatedAt = DateTime.UtcNow
        };

        _context.TournamentAnnouncements.Add(announcement);
        await _context.SaveChangesAsync();

        // Load creator user for DTO
        await _context.Entry(announcement).Reference(a => a.CreatedByUser).LoadAsync();

        // Send push notifications (don't fail if this errors)
        try
        {
            await SendAnnouncementNotificationsAsync(tournamentId, announcement.Id, request.Title, request.Body, request.Target ?? "All", request.TargetTeamIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notifications for announcement {AnnouncementId} in tournament {TournamentId}",
                announcement.Id, tournamentId);
        }

        return new TournamentAnnouncementDto(
            announcement.Id,
            announcement.TournamentId,
            announcement.Title,
            announcement.Body,
            announcement.Target,
            request.TargetTeamIds,
            announcement.CreatedByUserId,
            announcement.CreatedByUser.FirstName,
            announcement.CreatedByUser.LastName,
            announcement.CreatedAt,
            announcement.UpdatedAt
        );
    }

    public async Task<TournamentAnnouncementDto?> UpdateAnnouncementAsync(Guid tournamentId, Guid announcementId, UpdateTournamentAnnouncementRequest request, Guid requesterId)
    {
        // Check requester is admin
        var isAdmin = await _authService.IsAdminAsync(tournamentId, requesterId);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("Only tournament admins can update announcements.");
        }

        // Find announcement
        var announcement = await _context.TournamentAnnouncements
            .Include(a => a.CreatedByUser)
            .FirstOrDefaultAsync(a => a.Id == announcementId && a.TournamentId == tournamentId && a.DeletedAt == null);

        if (announcement == null)
        {
            return null;
        }

        // Update fields (PATCH semantics - only update provided fields)
        if (request.Title != null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new InvalidOperationException("Title cannot be empty.");
            }
            announcement.Title = request.Title;
        }

        if (request.Body != null)
        {
            if (string.IsNullOrWhiteSpace(request.Body))
            {
                throw new InvalidOperationException("Body cannot be empty.");
            }
            announcement.Body = request.Body;
        }

        // Handle Target update
        if (request.Target != null || request.TargetTeamIds != null)
        {
            announcement.Target = request.Target;
            announcement.TargetTeamIds = request.TargetTeamIds != null ? JsonSerializer.Serialize(request.TargetTeamIds) : null;
        }

        announcement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new TournamentAnnouncementDto(
            announcement.Id,
            announcement.TournamentId,
            announcement.Title,
            announcement.Body,
            announcement.Target,
            string.IsNullOrEmpty(announcement.TargetTeamIds) ? null : JsonSerializer.Deserialize<List<Guid>>(announcement.TargetTeamIds),
            announcement.CreatedByUserId,
            announcement.CreatedByUser.FirstName,
            announcement.CreatedByUser.LastName,
            announcement.CreatedAt,
            announcement.UpdatedAt
        );
    }

    public async Task<bool> DeleteAnnouncementAsync(Guid tournamentId, Guid announcementId, Guid requesterId)
    {
        // Check requester is admin
        var isAdmin = await _authService.IsAdminAsync(tournamentId, requesterId);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("Only tournament admins can delete announcements.");
        }

        // Find announcement
        var announcement = await _context.TournamentAnnouncements
            .FirstOrDefaultAsync(a => a.Id == announcementId && a.TournamentId == tournamentId && a.DeletedAt == null);

        if (announcement == null)
        {
            return false;
        }

        // Soft delete
        announcement.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Send push notifications to target users based on announcement target type.
    /// </summary>
    private async Task SendAnnouncementNotificationsAsync(
        Guid tournamentId,
        Guid announcementId,
        string title,
        string body,
        string target,
        List<Guid>? targetTeamIds)
    {
        // Determine target users based on announcement target
        var targetUserIds = new List<Guid>();

        switch (target)
        {
            case "All":
                // All users with registrations in the tournament
                targetUserIds = await _context.TournamentRegistrations
                    .Where(r => r.TournamentId == tournamentId)
                    .Select(r => r.UserId)
                    .Distinct()
                    .ToListAsync();
                break;

            case "Captains":
                // All team captains
                targetUserIds = await _context.TournamentTeams
                    .Where(t => t.TournamentId == tournamentId && t.CaptainUserId != null)
                    .Select(t => t.CaptainUserId!.Value)
                    .Distinct()
                    .ToListAsync();
                break;

            case "Admins":
                // All tournament admins (including creator)
                var tournament = await _context.Tournaments.FindAsync(tournamentId);
                if (tournament == null) return;

                var adminUserIds = await _context.TournamentAdmins
                    .Where(a => a.TournamentId == tournamentId)
                    .Select(a => a.UserId)
                    .ToListAsync();

                targetUserIds.Add(tournament.CreatorId);
                targetUserIds.AddRange(adminUserIds);
                targetUserIds = targetUserIds.Distinct().ToList();
                break;

            default:
                // Team-specific announcements
                if (targetTeamIds != null && targetTeamIds.Any())
                {
                    // Get all members of the targeted teams (including captains)
                    var teamMemberUserIds = await _context.TournamentTeamMembers
                        .Where(m => targetTeamIds.Contains(m.TeamId))
                        .Select(m => m.UserId)
                        .ToListAsync();

                    var captainUserIds = await _context.TournamentTeams
                        .Where(t => targetTeamIds.Contains(t.Id) && t.CaptainUserId != null)
                        .Select(t => t.CaptainUserId!.Value)
                        .ToListAsync();

                    targetUserIds.AddRange(teamMemberUserIds);
                    targetUserIds.AddRange(captainUserIds);
                    targetUserIds = targetUserIds.Distinct().ToList();
                }
                break;
        }

        if (!targetUserIds.Any())
        {
            _logger.LogInformation("No target users found for announcement {AnnouncementId} with target {Target}",
                announcementId, target);
            return;
        }

        // Get push tokens for target users
        var usersWithTokens = await _context.Users
            .Where(u => targetUserIds.Contains(u.Id) && !string.IsNullOrEmpty(u.PushToken))
            .Select(u => u.PushToken!)
            .ToListAsync();

        if (!usersWithTokens.Any())
        {
            _logger.LogInformation("No push tokens found for {Count} target users for announcement {AnnouncementId}",
                targetUserIds.Count, announcementId);
            return;
        }

        // Truncate body if too long (keep it under 200 characters for notification)
        var notificationBody = body.Length > 200 ? body.Substring(0, 197) + "..." : body;

        // Send batch push notifications
        _logger.LogInformation("Sending push notifications to {Count} users for announcement {AnnouncementId}",
            usersWithTokens.Count, announcementId);

        await _notificationService.SendBatchPushNotificationsAsync(
            usersWithTokens,
            title,
            notificationBody,
            new
            {
                tournamentId = tournamentId.ToString(),
                announcementId = announcementId.ToString(),
                type = "tournament_announcement"
            });
    }
}
