using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class EventService : IEventService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IOrganizationAdminService _adminService;
    private static readonly HashSet<string> ValidSkillLevels = new() { "Gold", "Silver", "Bronze", "D-League" };

    public EventService(AppDbContext context, INotificationService notificationService, IOrganizationAdminService adminService)
    {
        _context = context;
        _notificationService = notificationService;
        _adminService = adminService;
    }

    private void ValidateSkillLevels(List<string>? skillLevels)
    {
        if (skillLevels == null || skillLevels.Count == 0) return;

        foreach (var level in skillLevels)
        {
            if (!ValidSkillLevels.Contains(level))
            {
                throw new InvalidOperationException($"Invalid skill level: '{level}'. Valid values: Gold, Silver, Bronze, D-League");
            }
        }
    }

    /// <summary>
    /// Check if user can manage an event.
    /// For standalone events: only the creator can manage.
    /// For org-linked events: any org admin can manage.
    /// </summary>
    private async Task<bool> CanUserManageEventAsync(Event evt, Guid userId)
    {
        if (evt.OrganizationId.HasValue)
        {
            // Org-linked event: check if user is org admin
            return await _adminService.IsUserAdminAsync(evt.OrganizationId.Value, userId);
        }
        else
        {
            // Standalone event: only creator can manage
            return evt.CreatorId == userId;
        }
    }

    public async Task<EventDto> CreateAsync(CreateEventRequest request, Guid creatorId)
    {
        // Validate visibility rules
        var visibility = request.Visibility ?? "Public";
        if (visibility == "OrganizationMembers" && !request.OrganizationId.HasValue)
        {
            throw new InvalidOperationException("OrganizationMembers visibility requires an organization");
        }

        ValidateSkillLevels(request.SkillLevels);

        // Default duration to 60 minutes if not provided
        var duration = request.Duration ?? 60;

        var evt = new Event
        {
            OrganizationId = request.OrganizationId,
            CreatorId = creatorId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name,
            Description = request.Description,
            EventDate = request.EventDate,
            Duration = duration,
            Venue = request.Venue,
            MaxPlayers = request.MaxPlayers,
            Cost = request.Cost,
            RegistrationDeadline = request.RegistrationDeadline,
            Visibility = visibility,
            SkillLevels = request.SkillLevels
        };

        _context.Events.Add(evt);
        await _context.SaveChangesAsync();

        // Load organization for MapToDto if event belongs to one
        if (evt.OrganizationId.HasValue)
        {
            evt.Organization = await _context.Organizations.FindAsync(evt.OrganizationId.Value);

            // Notify organization subscribers about the new event
            var orgName = evt.Organization?.Name ?? "An organization";
            await _notificationService.NotifyOrganizationSubscribersAsync(
                evt.OrganizationId.Value,
                $"New Event: {evt.Name}",
                $"{orgName} posted a new event on {evt.EventDate:MMM d} at {evt.Venue ?? "TBD"}",
                new { eventId = evt.Id.ToString(), type = "new_event" }
            );
        }

        return await MapToDto(evt, creatorId);
    }

    public async Task<List<EventDto>> GetAllAsync(Guid? currentUserId = null)
    {
        // Get user's organization subscriptions for visibility filtering
        var userSubscribedOrgIds = currentUserId.HasValue
            ? await _context.OrganizationSubscriptions
                .Where(s => s.UserId == currentUserId.Value)
                .Select(s => s.OrganizationId)
                .ToListAsync()
            : new List<Guid>();

        var events = await _context.Events
            .Include(e => e.Organization)
            .Include(e => e.Registrations)
            .Where(e => e.Status != "Cancelled" && e.EventDate >= DateTime.UtcNow)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        // Filter by visibility
        var visibleEvents = new List<Event>();
        foreach (var evt in events)
        {
            if (await CanUserSeeEventAsync(evt, currentUserId, userSubscribedOrgIds))
            {
                visibleEvents.Add(evt);
            }
        }

        var dtos = new List<EventDto>();
        foreach (var evt in visibleEvents)
        {
            dtos.Add(await MapToDto(evt, currentUserId));
        }

        return dtos;
    }

    /// <summary>
    /// Determines if a user can see an event based on visibility rules.
    /// Phase B: Will add InviteOnly check against EventInvitation table.
    /// </summary>
    private async Task<bool> CanUserSeeEventAsync(Event evt, Guid? currentUserId, List<Guid> userSubscribedOrgIds)
    {
        // Creator can always see their own events
        if (currentUserId.HasValue && evt.CreatorId == currentUserId.Value)
        {
            return true;
        }

        // Org admins can always see their org's events
        if (currentUserId.HasValue && evt.OrganizationId.HasValue)
        {
            var isOrgAdmin = await _adminService.IsUserAdminAsync(evt.OrganizationId.Value, currentUserId.Value);
            if (isOrgAdmin)
            {
                return true;
            }
        }

        return evt.Visibility switch
        {
            "Public" => true,
            "OrganizationMembers" => evt.OrganizationId.HasValue && userSubscribedOrgIds.Contains(evt.OrganizationId.Value),
            "InviteOnly" => false, // Phase B: Check EventInvitation table
            _ => true // Default to visible for unknown visibility types
        };
    }

    public async Task<List<EventDto>> GetByOrganizationAsync(Guid organizationId, Guid? currentUserId = null)
    {
        // Check if user is subscribed to this organization
        var isSubscribed = currentUserId.HasValue &&
            await _context.OrganizationSubscriptions
                .AnyAsync(s => s.UserId == currentUserId.Value && s.OrganizationId == organizationId);

        var userSubscribedOrgIds = isSubscribed ? new List<Guid> { organizationId } : new List<Guid>();

        var events = await _context.Events
            .Include(e => e.Organization)
            .Include(e => e.Registrations)
            .Where(e => e.OrganizationId == organizationId && e.Status != "Cancelled")
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        // Filter by visibility
        var visibleEvents = new List<Event>();
        foreach (var evt in events)
        {
            if (await CanUserSeeEventAsync(evt, currentUserId, userSubscribedOrgIds))
            {
                visibleEvents.Add(evt);
            }
        }

        var dtos = new List<EventDto>();
        foreach (var evt in visibleEvents)
        {
            dtos.Add(await MapToDto(evt, currentUserId));
        }

        return dtos;
    }

    public async Task<EventDto?> GetByIdAsync(Guid id, Guid? currentUserId = null)
    {
        var evt = await _context.Events
            .Include(e => e.Organization)
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (evt == null) return null;

        // Check visibility
        var userSubscribedOrgIds = currentUserId.HasValue && evt.OrganizationId.HasValue
            ? await _context.OrganizationSubscriptions
                .Where(s => s.UserId == currentUserId.Value && s.OrganizationId == evt.OrganizationId.Value)
                .Select(s => s.OrganizationId)
                .ToListAsync()
            : new List<Guid>();

        if (!await CanUserSeeEventAsync(evt, currentUserId, userSubscribedOrgIds))
        {
            return null; // User cannot see this event
        }

        return await MapToDto(evt, currentUserId);
    }

    public async Task<EventDto?> UpdateAsync(Guid id, UpdateEventRequest request, Guid userId)
    {
        var evt = await _context.Events
            .Include(e => e.Organization)
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (evt == null) return null;

        // Check if user can manage this event
        if (!await CanUserManageEventAsync(evt, userId)) return null;

        if (request.Name != null) evt.Name = request.Name;
        if (request.Description != null) evt.Description = request.Description;
        if (request.EventDate.HasValue) evt.EventDate = request.EventDate.Value;
        if (request.Duration.HasValue) evt.Duration = request.Duration.Value;
        if (request.Venue != null) evt.Venue = request.Venue;
        if (request.MaxPlayers.HasValue) evt.MaxPlayers = request.MaxPlayers.Value;
        if (request.Cost.HasValue) evt.Cost = request.Cost.Value;
        if (request.RegistrationDeadline.HasValue) evt.RegistrationDeadline = request.RegistrationDeadline;
        if (request.Status != null) evt.Status = request.Status;

        // Handle visibility changes
        if (request.Visibility != null)
        {
            // Validate OrganizationMembers requires an organization
            if (request.Visibility == "OrganizationMembers" && !evt.OrganizationId.HasValue)
            {
                throw new InvalidOperationException("OrganizationMembers visibility requires an organization");
            }
            evt.Visibility = request.Visibility;
        }

        // Handle skill level changes
        if (request.SkillLevels != null)
        {
            ValidateSkillLevels(request.SkillLevels);
            evt.SkillLevels = request.SkillLevels;
        }

        evt.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await MapToDto(evt, userId);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        var evt = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == id);

        if (evt == null) return false;

        // Check if user can manage this event
        if (!await CanUserManageEventAsync(evt, userId)) return false;

        evt.Status = "Cancelled";
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RegisterAsync(Guid eventId, Guid userId, string? position = null)
    {
        var evt = await _context.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null) return false;

        // Check registration deadline
        if (evt.RegistrationDeadline.HasValue && evt.RegistrationDeadline.Value < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Registration deadline has passed");
        }

        // Get user to validate/determine position
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        // Determine the position to register with
        var registeredPosition = DetermineRegistrationPosition(user, position);

        var existingReg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId);

        // If already registered (not cancelled), return false
        if (existingReg != null && existingReg.Status == "Registered") return false;

        var registeredCount = evt.Registrations.Count(r => r.Status == "Registered");
        if (registeredCount >= evt.MaxPlayers)
        {
            throw new InvalidOperationException("Event is full");
        }

        // Determine team assignment (balanced by position - goalies with goalies, skaters with skaters)
        var teamAssignment = await DetermineTeamAssignmentAsync(eventId, registeredPosition);

        if (existingReg != null)
        {
            // Re-activate cancelled registration
            existingReg.Status = "Registered";
            existingReg.RegisteredAt = DateTime.UtcNow;
            existingReg.RegisteredPosition = registeredPosition;
            existingReg.TeamAssignment = teamAssignment;
            // Reset payment status for paid events
            existingReg.PaymentStatus = evt.Cost > 0 ? "Pending" : null;
            existingReg.PaymentMarkedAt = null;
            existingReg.PaymentVerifiedAt = null;
        }
        else
        {
            // Create new registration
            var registration = new EventRegistration
            {
                EventId = eventId,
                UserId = userId,
                RegisteredPosition = registeredPosition,
                TeamAssignment = teamAssignment,
                // Set payment status based on event cost
                PaymentStatus = evt.Cost > 0 ? "Pending" : null
            };
            _context.EventRegistrations.Add(registration);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Determines team assignment based on current team balance by position.
    /// Goalies are balanced with goalies, skaters with skaters.
    /// Assigns to the team with fewer players of the same position, defaulting to Black if equal.
    /// </summary>
    private async Task<string> DetermineTeamAssignmentAsync(Guid eventId, string registeredPosition)
    {
        var blackCount = await _context.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == "Registered"
                && r.TeamAssignment == "Black" && r.RegisteredPosition == registeredPosition);
        var whiteCount = await _context.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == "Registered"
                && r.TeamAssignment == "White" && r.RegisteredPosition == registeredPosition);

        return blackCount <= whiteCount ? "Black" : "White";
    }

    /// <summary>
    /// Determines which position to register with based on user's positions and the requested position.
    /// </summary>
    private string DetermineRegistrationPosition(Models.Entities.User user, string? requestedPosition)
    {
        // If user has no positions set up, they can't register
        if (user.Positions == null || user.Positions.Count == 0)
        {
            throw new InvalidOperationException("Please set up your positions in your profile before registering for events");
        }

        // If user has exactly one position, use it (ignore requestedPosition)
        if (user.Positions.Count == 1)
        {
            var singlePosition = user.Positions.Keys.First();
            return singlePosition == "goalie" ? "Goalie" : "Skater";
        }

        // User has multiple positions - they must specify which one
        if (string.IsNullOrEmpty(requestedPosition))
        {
            throw new InvalidOperationException("You have multiple positions. Please select which position you want to register as");
        }

        // Normalize and validate the requested position
        var normalizedPosition = requestedPosition.ToLowerInvariant();
        if (normalizedPosition != "goalie" && normalizedPosition != "skater")
        {
            throw new InvalidOperationException("Invalid position. Must be 'Goalie' or 'Skater'");
        }

        // Verify user has this position in their profile
        if (!user.Positions.ContainsKey(normalizedPosition))
        {
            throw new InvalidOperationException($"You don't have {requestedPosition} in your profile positions");
        }

        return normalizedPosition == "goalie" ? "Goalie" : "Skater";
    }

    public async Task<bool> CancelRegistrationAsync(Guid eventId, Guid userId)
    {
        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId);

        if (registration == null) return false;

        registration.Status = "Cancelled";
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<EventRegistrationDto>> GetRegistrationsAsync(Guid eventId)
    {
        var registrations = await _context.EventRegistrations
            .Include(r => r.User)
            .Where(r => r.EventId == eventId && r.Status == "Registered")
            .ToListAsync();

        return registrations.Select(r => new EventRegistrationDto(
            r.Id,
            r.EventId,
            new UserDto(
                r.User.Id,
                r.User.Email,
                r.User.FirstName,
                r.User.LastName,
                r.User.PhoneNumber,
                r.User.Positions,
                r.User.VenmoHandle,
                r.User.Role,
                r.User.CreatedAt
            ),
            r.Status,
            r.RegisteredAt,
            r.RegisteredPosition,   // Position tracking
            r.PaymentStatus,        // Phase 4
            r.PaymentMarkedAt,      // Phase 4
            r.PaymentVerifiedAt,    // Phase 4
            r.TeamAssignment        // Team assignment
        )).ToList();
    }

    public async Task<List<EventDto>> GetUserRegistrationsAsync(Guid userId)
    {
        var registrations = await _context.EventRegistrations
            .Include(r => r.Event)
            .ThenInclude(e => e.Organization)
            .Include(r => r.Event.Registrations)
            .Where(r => r.UserId == userId && r.Status == "Registered")
            .Where(r => r.Event.EventDate >= DateTime.UtcNow)
            .OrderBy(r => r.Event.EventDate)
            .ToListAsync();

        var dtos = new List<EventDto>();
        foreach (var reg in registrations)
        {
            dtos.Add(await MapToDto(reg.Event, userId));
        }

        return dtos;
    }

    private async Task<EventDto> MapToDto(Event evt, Guid? currentUserId)
    {
        var registeredCount = evt.Registrations?.Count(r => r.Status == "Registered") ??
            await _context.EventRegistrations.CountAsync(r => r.EventId == evt.Id && r.Status == "Registered");

        var isRegistered = currentUserId.HasValue &&
            (evt.Registrations?.Any(r => r.UserId == currentUserId.Value && r.Status == "Registered") ??
             await _context.EventRegistrations.AnyAsync(r => r.EventId == evt.Id && r.UserId == currentUserId.Value && r.Status == "Registered"));

        // Organization name is null for standalone events
        // Note: Callers should ensure Organization is included when OrganizationId is set
        string? orgName = evt.OrganizationId.HasValue ? evt.Organization?.Name : null;

        // Check if user can manage this event (creator for standalone, org admin for org events)
        var canManage = currentUserId.HasValue && await CanUserManageEventAsync(evt, currentUserId.Value);

        // Get creator's Venmo handle for payment (Phase 4)
        string? creatorVenmoHandle = null;
        if (evt.Cost > 0)
        {
            var creator = await _context.Users.FindAsync(evt.CreatorId);
            creatorVenmoHandle = creator?.VenmoHandle;
        }

        // Get current user's registration info (payment status and team assignment)
        string? myPaymentStatus = null;
        string? myTeamAssignment = null;
        if (currentUserId.HasValue && isRegistered)
        {
            var myRegistration = evt.Registrations?.FirstOrDefault(r => r.UserId == currentUserId.Value && r.Status == "Registered")
                ?? await _context.EventRegistrations.FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == currentUserId.Value && r.Status == "Registered");
            if (myRegistration != null)
            {
                myPaymentStatus = evt.Cost > 0 ? myRegistration.PaymentStatus : null;
                myTeamAssignment = myRegistration.TeamAssignment;
            }
        }

        // Calculate unpaid count for organizers (paid events only)
        int? unpaidCount = null;
        if (canManage && evt.Cost > 0)
        {
            unpaidCount = evt.Registrations?.Count(r => r.Status == "Registered" && r.PaymentStatus != "Verified") ??
                await _context.EventRegistrations.CountAsync(r => r.EventId == evt.Id && r.Status == "Registered" && r.PaymentStatus != "Verified");
        }

        return new EventDto(
            evt.Id,
            evt.OrganizationId,
            orgName,
            evt.CreatorId,
            evt.Name,
            evt.Description,
            evt.EventDate,
            evt.Duration,
            evt.Venue,
            evt.MaxPlayers,
            registeredCount,
            evt.Cost,
            evt.RegistrationDeadline,
            evt.Status,
            evt.Visibility,
            evt.SkillLevels,     // Event's skill levels (can override org's)
            isRegistered,
            canManage,
            evt.CreatedAt,
            creatorVenmoHandle,  // Phase 4
            myPaymentStatus,     // Phase 4
            myTeamAssignment,    // Team assignment
            unpaidCount          // Organizer view
        );
    }

    // Payment methods (Phase 4)
    public async Task<bool> MarkPaymentAsync(Guid eventId, Guid userId, string? paymentReference)
    {
        var registration = await _context.EventRegistrations
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId && r.Status == "Registered");

        if (registration == null) return false;

        // Can't mark payment for free events
        if (registration.Event.Cost <= 0) return false;

        // Can only mark as paid if currently Pending
        if (registration.PaymentStatus != "Pending") return false;

        registration.PaymentStatus = "MarkedPaid";
        registration.PaymentMarkedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdatePaymentStatusAsync(Guid eventId, Guid registrationId, string paymentStatus, Guid organizerId)
    {
        // Verify the organizer can manage this event
        var evt = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (evt == null) return false;

        if (!await CanUserManageEventAsync(evt, organizerId)) return false;

        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.EventId == eventId && r.Status == "Registered");

        if (registration == null) return false;

        // Validate status transition
        if (paymentStatus != "Verified" && paymentStatus != "Pending")
        {
            throw new InvalidOperationException("Invalid payment status. Must be 'Verified' or 'Pending'");
        }

        registration.PaymentStatus = paymentStatus;

        if (paymentStatus == "Verified")
        {
            registration.PaymentVerifiedAt = DateTime.UtcNow;
        }
        else
        {
            // Reset if setting back to Pending
            registration.PaymentVerifiedAt = null;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    // Team assignment methods
    public async Task<bool> UpdateTeamAssignmentAsync(Guid eventId, Guid registrationId, string teamAssignment, Guid organizerId)
    {
        // Validate team assignment value
        if (teamAssignment != "Black" && teamAssignment != "White")
        {
            throw new InvalidOperationException("Invalid team. Must be 'Black' or 'White'");
        }

        // Verify the organizer can manage this event
        var evt = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (evt == null) return false;

        if (!await CanUserManageEventAsync(evt, organizerId)) return false;

        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.EventId == eventId && r.Status == "Registered");

        if (registration == null) return false;

        registration.TeamAssignment = teamAssignment;
        await _context.SaveChangesAsync();

        return true;
    }
}
