using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public interface IPastGameService
{
    Task<CreatePastGameResponse?> CreatePastGameAsync(Guid organizationId, CreatePastGameRequest request, Guid callerId);
}

/// <summary>
/// Adds a game that was PLAYED but never existed in the app (2026-11-22 was
/// invite only, organized outside it), so its result has an event to hang on.
///
/// Why not POST /api/events: EventService.CreateAsync sends the organization's
/// "New Game" push + notification-center blast to every subscriber (unless the
/// event starts as a Draft), applies the auto-roster (which notifies each
/// regular), and a Draft can only be made visible through PublishEventAsync,
/// which sends the same blast. Adding players goes through AddUserToWaitlistAsync
/// (notifies once the roster is published) and the roster becomes visible only
/// through PublishRosterAsync (pushes every registered player). There is no
/// silent path through the existing endpoints.
///
/// This writer touches ONLY the Events and EventRegistrations tables and calls
/// nothing else: no INotificationService, no auto-roster, no waitlist, no
/// payment (Cost 0, PaymentStatus null), no reminder (every background job
/// selects EventDate in the future; this one is in the past), no badge.
/// Idempotent: a second call for the same organization and EventDate returns
/// the existing event ("already_exists") and writes nothing.
/// </summary>
public class PastGameService : IPastGameService
{
    private static readonly string[] Visibilities = { "Public", "OrganizationMembers", "InviteOnly" };

    private readonly AppDbContext _context;
    private readonly IOrganizationAdminService _adminService;
    private readonly ILogger<PastGameService> _logger;

    public PastGameService(AppDbContext context, IOrganizationAdminService adminService, ILogger<PastGameService> logger)
    {
        _context = context;
        _adminService = adminService;
        _logger = logger;
    }

    public async Task<CreatePastGameResponse?> CreatePastGameAsync(Guid organizationId, CreatePastGameRequest request, Guid callerId)
    {
        if (!await _context.Organizations.AnyAsync(o => o.Id == organizationId)) return null;
        if (!await _adminService.IsUserAdminAsync(organizationId, callerId))
        {
            _logger.LogWarning("Past game denied for organization {OrganizationId}: {UserId} is not an admin", organizationId, callerId);
            throw new UnauthorizedAccessException("Only an organization admin can add a past game");
        }

        var problem = Validate(request);
        if (problem != null)
        {
            _logger.LogWarning("Past game refused for organization {OrganizationId}: {Problem}", organizationId, problem);
            throw new InvalidOperationException(problem);
        }

        var eventDate = DateTime.SpecifyKind(request.EventDate.ToUniversalTime(), DateTimeKind.Utc);
        var existing = await _context.Events
            .Where(e => e.OrganizationId == organizationId && e.EventDate == eventDate && e.Status != "Cancelled")
            .Select(e => new { e.Id, Count = e.Registrations.Count(r => r.Status == "Registered") })
            .FirstOrDefaultAsync();
        if (existing != null)
        {
            _logger.LogInformation("Past game {GameKey} already exists as event {EventId}", request.SourceGameKey, existing.Id);
            return new CreatePastGameResponse(existing.Id, "already_exists", existing.Count, new List<Guid>());
        }

        var ids = request.Roster.Select(r => r.UserId).Distinct().ToList();
        var eligible = await _context.Users
            .Where(u => ids.Contains(u.Id) && u.IsActive && !u.IsGhostPlayer)
            .Select(u => u.Id).ToListAsync();
        var skipped = ids.Where(id => !eligible.Contains(id)).ToList();

        var now = DateTime.UtcNow;
        var evt = new Event
        {
            OrganizationId = organizationId,
            CreatorId = callerId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
            Description = request.Description,
            EventDate = eventDate,
            Duration = request.Duration ?? 60,
            Venue = request.Venue,
            MaxPlayers = Math.Max(eligible.Count, 1),
            Cost = 0,
            Status = "Published",               // exactly what every other past game looks like
            Visibility = request.Visibility ?? "OrganizationMembers",
            IsRosterPublished = true,           // teams visible; nothing is sent (no publish call)
            PublishedAt = eventDate,
            // Mark every reminder as already sent, belt and braces: the jobs select
            // future events only, and this one never is.
            PlayerReminderSentAt = now,
            OrganizerPaymentReminderSentAt = now,
            OrganizerPublishReminder24hSentAt = now,
            OrganizerPublishReminder8hSentAt = now,
            OrganizerPublishReminder5hSentAt = now,
            NotFullReminderSentAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _context.Events.Add(evt);

        var order = new Dictionary<string, int> { ["Black"] = 0, ["White"] = 0 };
        foreach (var entry in request.Roster.Where(r => eligible.Contains(r.UserId)).DistinctBy(r => r.UserId))
        {
            var team = entry.Team.Trim().Equals("black", StringComparison.OrdinalIgnoreCase) ? "Black" : "White";
            _context.EventRegistrations.Add(new EventRegistration
            {
                EventId = evt.Id,
                UserId = entry.UserId,
                Status = "Registered",
                RegisteredAt = eventDate,
                RegisteredPosition = entry.Position.Trim().Equals("goalie", StringComparison.OrdinalIgnoreCase) ? "Goalie" : "Skater",
                PaymentStatus = null,
                TeamAssignment = team,
                RosterOrder = ++order[team],
            });
        }
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Past game {GameKey} created as event {EventId} in organization {OrganizationId} by {UserId}: {Count} registered, {Skipped} skipped, no notifications",
            request.SourceGameKey, evt.Id, organizationId, callerId, eligible.Count, skipped.Count);
        return new CreatePastGameResponse(evt.Id, "created", eligible.Count, skipped);
    }

    public static string? Validate(CreatePastGameRequest r)
    {
        if (r == null) return "Missing body";
        if (string.IsNullOrWhiteSpace(r.SourceGameKey) || r.SourceGameKey.Length > 100)
            return "sourceGameKey is required (at most 100 characters)";
        if (r.EventDate.Kind == DateTimeKind.Unspecified)
            return "eventDate must carry a time zone (UTC, e.g. 2026-11-22T21:00:00Z)";
        var utc = r.EventDate.ToUniversalTime();
        if (utc > DateTime.UtcNow.AddHours(-1)) return "A past game must have been played (eventDate in the past)";
        var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, GameResultSettings.SeasonTimeZone));
        if (local < GameResultSettings.EarliestGameDate)
            return $"Past games start with {GameResultSettings.EarliestGameDate:yyyy-MM-dd}";
        if (r.Name is { Length: > 200 }) return "name is at most 200 characters";
        if (r.Duration is < 1 or > 600) return "duration must be 1-600 minutes";
        if (r.Visibility != null && !Visibilities.Contains(r.Visibility)) return "visibility must be Public, OrganizationMembers or InviteOnly";
        if (r.Roster == null || r.Roster.Count == 0 || r.Roster.Count > 80) return "roster needs 1-80 entries";
        foreach (var e in r.Roster)
        {
            var team = e?.Team?.Trim().ToLowerInvariant();
            var pos = e?.Position?.Trim().ToLowerInvariant();
            if (e == null || e.UserId == Guid.Empty) return "Every roster entry needs a userId";
            if (team is not ("black" or "white")) return "Every roster entry's team must be Black or White";
            if (pos is not ("skater" or "goalie")) return "Every roster entry's position must be Skater or Goalie";
        }
        return null;
    }
}
