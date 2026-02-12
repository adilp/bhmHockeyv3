using System.Data;
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
    private readonly IWaitlistService _waitlistService;
    private readonly ILogger<EventService> _logger;
    private static readonly HashSet<string> ValidSkillLevels = new() { "Gold", "Silver", "Bronze", "D-League" };

    // Central Time Zone for displaying times to users (local community app)
    private static readonly TimeZoneInfo CentralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    public EventService(
        AppDbContext context,
        INotificationService notificationService,
        IOrganizationAdminService adminService,
        IWaitlistService waitlistService,
        ILogger<EventService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _adminService = adminService;
        _waitlistService = waitlistService;
        _logger = logger;
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
    /// Returns true if roster-related notifications should be sent for this event.
    /// Notifications are suppressed while roster is unpublished (draft mode).
    /// </summary>
    private bool ShouldSendRosterNotification(Event evt)
    {
        return evt.IsRosterPublished;
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

    /// <summary>
    /// Public method to check if user can manage an event by ID.
    /// Used by controller for authorization checks.
    /// </summary>
    public async Task<bool> CanUserManageEventAsync(Guid eventId, Guid userId)
    {
        var evt = await _context.Events.FindAsync(eventId);
        if (evt == null) return false;

        return await CanUserManageEventAsync(evt, userId);
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

            // Notify organization subscribers about the new game
            var orgName = evt.Organization?.Name ?? "An organization";
            var venueText = !string.IsNullOrWhiteSpace(evt.Venue) ? $" - {evt.Venue}" : "";
            // Convert UTC to Central Time for display
            var localEventDate = TimeZoneInfo.ConvertTimeFromUtc(evt.EventDate, CentralTimeZone);
            await _notificationService.NotifyOrganizationSubscribersAsync(
                evt.OrganizationId.Value,
                $"New Game: {evt.Name}",
                $"{orgName} posted a new game on {localEventDate:MMM d} at {localEventDate:h:mm tt}{venueText}",
                new { eventId = evt.Id.ToString(), type = "new_event" },
                type: "new_event",
                eventId: evt.Id
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

        // Handle slot position label changes
        if (request.SlotPositionLabels != null)
        {
            evt.SlotPositionLabels = request.SlotPositionLabels;
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

    public async Task<RegistrationResultDto> RegisterAsync(Guid eventId, Guid userId, string? position = null)
    {
        var evt = await _context.Events
            .Include(e => e.Registrations)
            .Include(e => e.Creator)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null)
        {
            throw new InvalidOperationException("Event not found");
        }

        // Check registration deadline
        if (evt.RegistrationDeadline.HasValue && evt.RegistrationDeadline.Value < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Registration deadline has passed");
        }

        // Get user to validate/determine position
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Determine the position to register with
        var registeredPosition = DetermineRegistrationPosition(user, position);

        var existingReg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId);

        // If already registered (not cancelled or waitlisted), throw error
        if (existingReg != null && (existingReg.Status == "Registered" || existingReg.Status == "Waitlisted"))
        {
            throw new InvalidOperationException("Already registered for this event");
        }

        // Goalies don't count against MaxPlayers - only skaters do
        var skaterCount = evt.Registrations.Count(r => r.Status == "Registered" && r.RegisteredPosition != "Goalie");
        bool isFull = registeredPosition == "Goalie" ? false : skaterCount >= evt.MaxPlayers;
        bool isPaidEvent = evt.Cost > 0;

        // Paid events: ALWAYS waitlist first (organizer verifies payment before adding to roster)
        // Free events: Only waitlist if roster is full
        if (isPaidEvent || isFull)
        {
            // Add to waitlist
            var waitlistPosition = await _waitlistService.GetNextWaitlistPositionAsync(eventId);

            if (existingReg != null)
            {
                // Re-activate cancelled registration as waitlisted
                existingReg.Status = "Waitlisted";
                existingReg.RegisteredAt = DateTime.UtcNow;
                existingReg.RegisteredPosition = registeredPosition;
                existingReg.WaitlistPosition = waitlistPosition;
                existingReg.TeamAssignment = null; // No team until promoted
                existingReg.PaymentStatus = isPaidEvent ? "Pending" : null;
                existingReg.PaymentMarkedAt = null;
                existingReg.PaymentVerifiedAt = null;
                existingReg.PromotedAt = null;
                existingReg.PaymentDeadlineAt = null;
            }
            else
            {
                // Create new waitlisted registration
                var registration = new EventRegistration
                {
                    EventId = eventId,
                    UserId = userId,
                    Status = "Waitlisted",
                    RegisteredPosition = registeredPosition,
                    WaitlistPosition = waitlistPosition,
                    PaymentStatus = isPaidEvent ? "Pending" : null
                };
                _context.EventRegistrations.Add(registration);
            }

            await _context.SaveChangesAsync();

            // Notify organizer about new waitlist signup
            await NotifyOrganizerNewWaitlistSignupAsync(evt, user, waitlistPosition);

            // Different message for paid events vs full free events
            var message = isPaidEvent
                ? "You've been added to the waitlist. Please mark your payment so the organizer can verify and add you to the roster."
                : $"Event is full. You're #{waitlistPosition} on the waitlist.";

            return new RegistrationResultDto(
                "Waitlisted",
                waitlistPosition,
                message
            );
        }
        else
        {
            // Free event with available capacity: register directly
            var teamAssignment = await DetermineTeamAssignmentAsync(eventId, registeredPosition);

            if (existingReg != null)
            {
                // Re-activate cancelled registration
                existingReg.Status = "Registered";
                existingReg.RegisteredAt = DateTime.UtcNow;
                existingReg.RegisteredPosition = registeredPosition;
                existingReg.TeamAssignment = teamAssignment;
                existingReg.WaitlistPosition = null;
                existingReg.PaymentStatus = null; // Free event, no payment tracking
                existingReg.PaymentMarkedAt = null;
                existingReg.PaymentVerifiedAt = null;
                existingReg.PromotedAt = null;
                existingReg.PaymentDeadlineAt = null;
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
                    PaymentStatus = null // Free event, no payment tracking
                };
                _context.EventRegistrations.Add(registration);
            }

            await _context.SaveChangesAsync();

            return new RegistrationResultDto(
                "Registered",
                null,
                "Successfully registered!"
            );
        }
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

        var wasRegistered = registration.Status == "Registered";
        var wasWaitlisted = registration.Status == "Waitlisted";

        // Use Serializable transaction to atomically cancel and promote
        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        PromotionResult? promotionResult = null;

        try
        {
            registration.Status = "Cancelled";
            registration.WaitlistPosition = null;
            registration.PaymentDeadlineAt = null;
            await _context.SaveChangesAsync();

            // If a registered user cancels, promote next from waitlist
            if (wasRegistered)
            {
                promotionResult = await _waitlistService.PromoteFromWaitlistAsync(
                    eventId,
                    spotCount: 1,
                    callerOwnsTransaction: true  // We manage the transaction
                );
            }
            // If a waitlisted user cancels, renumber the waitlist
            else if (wasWaitlisted)
            {
                await _waitlistService.UpdateWaitlistPositionsAsync(eventId);
            }

            await transaction.CommitAsync();

            // Send notifications AFTER commit to prevent false notifications on rollback
            if (promotionResult != null)
            {
                await _waitlistService.SendPendingNotificationsAsync(
                    promotionResult.PendingNotifications);
            }

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<EventRegistrationDto>> GetRegistrationsAsync(Guid eventId)
    {
        // Include both registered and waitlisted users (exclude cancelled)
        // Order by RosterOrder (null values last), then by RegisteredAt
        var registrations = await _context.EventRegistrations
            .Include(r => r.User)
            .Where(r => r.EventId == eventId && (r.Status == "Registered" || r.Status == "Waitlisted"))
            .OrderBy(r => r.RosterOrder == null ? 1 : 0)
            .ThenBy(r => r.RosterOrder)
            .ThenBy(r => r.RegisteredAt)
            .ToListAsync();

        // Batch load badges for all users to prevent N+1 queries
        var userIds = registrations.Select(r => r.User.Id).Distinct();
        var badgesByUser = await GetBadgesForUsersAsync(userIds);

        return registrations.Select(r => {
            var (topBadges, totalCount) = badgesByUser.TryGetValue(r.User.Id, out var badges)
                ? badges
                : (new List<UserBadgeDto>(), 0);

            return new EventRegistrationDto(
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
                    r.User.CreatedAt,
                    topBadges,      // Top 3 badges
                    totalCount,     // Total badge count
                    r.User.IsGhostPlayer
                ),
                r.Status,
                r.RegisteredAt,
                r.RegisteredPosition,   // Position tracking
                r.PaymentStatus,        // Phase 4
                r.PaymentMarkedAt,      // Phase 4
                r.PaymentVerifiedAt,    // Phase 4
                r.TeamAssignment,       // Team assignment
                r.RosterOrder,          // Roster ordering
                r.WaitlistPosition,     // Phase 5 - Waitlist
                r.PromotedAt,           // Phase 5 - Waitlist
                r.PaymentDeadlineAt,    // Phase 5 - Waitlist
                r.IsWaitlisted          // Phase 5 - Waitlist
            );
        }).ToList();
    }

    public async Task<List<EventRegistrationDto>> GetWaitlistWithBadgesAsync(Guid eventId)
    {
        var waitlist = await _waitlistService.GetWaitlistAsync(eventId);

        // Batch load badges for all users to prevent N+1 queries
        var userIds = waitlist.Select(r => r.User.Id).Distinct();
        var badgesByUser = await GetBadgesForUsersAsync(userIds);

        return waitlist.Select(r => {
            var (topBadges, totalCount) = badgesByUser.TryGetValue(r.User.Id, out var badges)
                ? badges
                : (new List<UserBadgeDto>(), 0);

            return new EventRegistrationDto(
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
                    r.User.CreatedAt,
                    topBadges,      // Top 3 badges
                    totalCount,     // Total badge count
                    r.User.IsGhostPlayer
                ),
                r.Status,
                r.RegisteredAt,
                r.RegisteredPosition,
                r.PaymentStatus,
                r.PaymentMarkedAt,
                r.PaymentVerifiedAt,
                r.TeamAssignment,
                r.RosterOrder,
                r.WaitlistPosition,
                r.PromotedAt,
                r.PaymentDeadlineAt,
                r.IsWaitlisted
            );
        }).ToList();
    }

    // Update roster order for multiple registrations (batch update)
    public async Task<bool> UpdateRosterOrderAsync(Guid eventId, List<RosterOrderItem> items, Guid organizerId)
    {
        // Verify user can manage this event
        var canManage = await CanUserManageEventAsync(eventId, organizerId);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("You don't have permission to manage this event");
        }

        // Get all registrations for this event
        var registrations = await _context.EventRegistrations
            .Where(r => r.EventId == eventId && r.Status == "Registered")
            .ToListAsync();

        // Update each registration
        foreach (var item in items)
        {
            var registration = registrations.FirstOrDefault(r => r.Id == item.RegistrationId);
            if (registration != null)
            {
                // Validate team assignment
                if (item.TeamAssignment != "Black" && item.TeamAssignment != "White")
                {
                    throw new InvalidOperationException($"Invalid team assignment: {item.TeamAssignment}");
                }
                registration.TeamAssignment = item.TeamAssignment;
                registration.RosterOrder = item.RosterOrder;
            }
        }

        await _context.SaveChangesAsync();
        return true;
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
        // Include both registered AND waitlisted users for payment status
        string? myPaymentStatus = null;
        string? myTeamAssignment = null;
        if (currentUserId.HasValue)
        {
            var myRegistration = evt.Registrations?.FirstOrDefault(r => r.UserId == currentUserId.Value && (r.Status == "Registered" || r.Status == "Waitlisted"))
                ?? await _context.EventRegistrations.FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == currentUserId.Value && (r.Status == "Registered" || r.Status == "Waitlisted"));
            if (myRegistration != null)
            {
                // Payment status always visible (players need to know their payment state during draft)
                myPaymentStatus = evt.Cost > 0 ? myRegistration.PaymentStatus : null;

                // Team assignment only visible if roster is published OR user can manage
                if (evt.IsRosterPublished || canManage)
                {
                    myTeamAssignment = myRegistration.TeamAssignment;
                }
            }
        }

        // Calculate unpaid count for organizers (paid events only)
        // Includes both registered and waitlisted users with unverified payments
        int? unpaidCount = null;
        if (canManage && evt.Cost > 0)
        {
            unpaidCount = evt.Registrations?.Count(r => (r.Status == "Registered" || r.Status == "Waitlisted") && r.PaymentStatus != "Verified") ??
                await _context.EventRegistrations.CountAsync(r => r.EventId == evt.Id && (r.Status == "Registered" || r.Status == "Waitlisted") && r.PaymentStatus != "Verified");
        }

        // Waitlist fields (Phase 5)
        var waitlistCount = evt.Registrations?.Count(r => r.Status == "Waitlisted") ??
            await _context.EventRegistrations.CountAsync(r => r.EventId == evt.Id && r.Status == "Waitlisted");

        int? myWaitlistPosition = null;
        DateTime? myPaymentDeadline = null;
        bool amIWaitlisted = false;
        if (currentUserId.HasValue)
        {
            var myReg = evt.Registrations?.FirstOrDefault(r => r.UserId == currentUserId.Value && r.Status != "Cancelled")
                ?? await _context.EventRegistrations.FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == currentUserId.Value && r.Status != "Cancelled");
            if (myReg != null)
            {
                // Payment deadline always visible (players need deadlines during draft)
                myPaymentDeadline = myReg.PaymentDeadlineAt;
                amIWaitlisted = myReg.Status == "Waitlisted";

                // Waitlist position only visible if roster is published OR user can manage
                if (evt.IsRosterPublished || canManage)
                {
                    myWaitlistPosition = myReg.WaitlistPosition;
                }
            }
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
            evt.IsRosterPublished,  // Roster draft mode
            creatorVenmoHandle,  // Phase 4
            myPaymentStatus,     // Phase 4
            myTeamAssignment,    // Team assignment
            unpaidCount,         // Organizer view
            waitlistCount,       // Phase 5 - Waitlist
            myWaitlistPosition,  // Phase 5 - Waitlist
            myPaymentDeadline,   // Phase 5 - Waitlist
            amIWaitlisted,       // Phase 5 - Waitlist
            evt.SlotPositionLabels  // Slot position labels
        );
    }

    // Payment methods (Phase 4)
    public async Task<bool> MarkPaymentAsync(Guid eventId, Guid userId, string? paymentReference)
    {
        var registration = await _context.EventRegistrations
            .Include(r => r.Event)
                .ThenInclude(e => e.Creator)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId && (r.Status == "Registered" || r.Status == "Waitlisted"));

        if (registration == null) return false;

        // Can't mark payment for free events
        if (registration.Event.Cost <= 0) return false;

        // Can only mark as paid if currently Pending
        if (registration.PaymentStatus != "Pending") return false;

        registration.PaymentStatus = "MarkedPaid";
        registration.PaymentMarkedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Notify the organizer that a user marked their payment
        if (!string.IsNullOrEmpty(registration.Event.Creator?.PushToken))
        {
            var userName = $"{registration.User.FirstName} {registration.User.LastName}".Trim();
            if (string.IsNullOrEmpty(userName)) userName = registration.User.Email;
            var eventName = registration.Event.Name ?? $"Event on {registration.Event.EventDate:MMM d}";

            await _notificationService.SendPushNotificationAsync(
                registration.Event.Creator.PushToken,
                "Payment Pending Verification",
                $"{userName} marked their payment for {eventName}. Tap to view.",
                new Dictionary<string, string>
                {
                    { "type", "payment_marked" },
                    { "eventId", eventId.ToString() }
                }
            );
        }

        return true;
    }

    public async Task<PaymentUpdateResultDto> UpdatePaymentStatusAsync(Guid eventId, Guid registrationId, string paymentStatus, Guid organizerId)
    {
        // Verify the organizer can manage this event
        var evt = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (evt == null)
        {
            _logger.LogWarning("UpdatePaymentStatus failed: event {EventId} not found", eventId);
            return new PaymentUpdateResultDto(false, false, "Event not found", null);
        }

        if (!await CanUserManageEventAsync(evt, organizerId))
        {
            _logger.LogWarning("UpdatePaymentStatus denied: user {OrganizerId} not authorized for event {EventId}", organizerId, eventId);
            return new PaymentUpdateResultDto(false, false, "You are not authorized to manage this event", null);
        }

        // Find registration - include both registered and waitlisted users
        var registration = await _context.EventRegistrations
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.EventId == eventId
                && (r.Status == "Registered" || r.Status == "Waitlisted"));

        if (registration == null)
        {
            _logger.LogWarning("UpdatePaymentStatus failed: registration {RegistrationId} not found for event {EventId}", registrationId, eventId);
            return new PaymentUpdateResultDto(false, false, "Registration not found", null);
        }

        // Validate status transition
        if (paymentStatus != "Verified" && paymentStatus != "Pending")
        {
            throw new InvalidOperationException("Invalid payment status. Must be 'Verified' or 'Pending'");
        }

        string message;
        bool promoted = false;

        // Track notification action to send AFTER commit
        // 0 = none, 1 = promoted from waitlist, 2 = verified but roster full
        int notificationAction = 0;

        if (paymentStatus == "Verified")
        {
            registration.PaymentStatus = "Verified";
            registration.PaymentVerifiedAt = DateTime.UtcNow;

            // If user is waitlisted, check if we can promote them
            if (registration.Status == "Waitlisted")
            {
                // Use Serializable transaction for the critical section to prevent race conditions
                await using var transaction = await _context.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable);

                try
                {
                    // Check roster capacity (goalies don't count against MaxPlayers)
                    var isGoalie = registration.RegisteredPosition == "Goalie";
                    var skaterCount = await _context.EventRegistrations
                        .CountAsync(r => r.EventId == eventId && r.Status == "Registered" && r.RegisteredPosition != "Goalie");

                    if (isGoalie || skaterCount < evt.MaxPlayers)
                    {
                        // Capacity available - promote user
                        registration.Status = "Registered";
                        registration.PromotedAt = DateTime.UtcNow;
                        registration.TeamAssignment = await DetermineTeamAssignmentAsync(eventId, registration.RegisteredPosition);
                        registration.WaitlistPosition = null;
                        registration.PaymentDeadlineAt = null; // Clear any deadline

                        promoted = true;
                        message = "Payment verified - user promoted to roster";
                        notificationAction = 1; // promoted from waitlist
                    }
                    else
                    {
                        // Roster full - keep waitlisted but mark as verified
                        message = "Payment verified - roster full, user remains on waitlist";
                        notificationAction = 2; // verified but roster full
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Reload entity to get fresh state after transaction
                    await _context.Entry(registration).ReloadAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            else
            {
                // User is already registered, just verify payment
                message = "Payment verified successfully";
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            // Reset to Pending
            registration.PaymentStatus = "Pending";
            registration.PaymentVerifiedAt = null;
            message = "Payment status reset to pending";
            await _context.SaveChangesAsync();
        }

        // Send notifications AFTER transaction commit
        if (notificationAction == 1)
        {
            // User was promoted - send notification and update waitlist positions
            await NotifyUserPromotedFromWaitlistAsync(evt, registration.User);
            await _waitlistService.UpdateWaitlistPositionsAsync(eventId);
        }
        else if (notificationAction == 2)
        {
            // Payment verified but roster full
            await NotifyUserPaymentVerifiedButFullAsync(evt, registration.User);
        }

        // Create the registration DTO for response
        var registrationDto = new EventRegistrationDto(
            registration.Id,
            registration.EventId,
            new UserDto(
                registration.User.Id,
                registration.User.Email,
                registration.User.FirstName,
                registration.User.LastName,
                registration.User.PhoneNumber,
                registration.User.Positions,
                registration.User.VenmoHandle,
                registration.User.Role,
                registration.User.CreatedAt,
                null,  // Badges not included in payment update response
                0,     // Total badge count not included
                registration.User.IsGhostPlayer
            ),
            registration.Status,
            registration.RegisteredAt,
            registration.RegisteredPosition,
            registration.PaymentStatus,
            registration.PaymentMarkedAt,
            registration.PaymentVerifiedAt,
            registration.TeamAssignment,
            registration.RosterOrder,
            registration.WaitlistPosition,
            registration.PromotedAt,
            registration.PaymentDeadlineAt,
            registration.IsWaitlisted
        );

        return new PaymentUpdateResultDto(true, promoted, message, registrationDto);
    }

    /// <summary>
    /// Notify user that their payment was verified but the roster is full.
    /// They remain on the waitlist with priority.
    /// </summary>
    private async Task NotifyUserPaymentVerifiedButFullAsync(Event evt, User user)
    {
        // Don't notify about waitlist status during draft mode
        if (!ShouldSendRosterNotification(evt))
        {
            return;
        }

        if (string.IsNullOrEmpty(user.PushToken))
        {
            return;
        }

        var eventName = evt.Name ?? $"Event on {evt.EventDate:MMM d}";

        await _notificationService.SendPushNotificationAsync(
            user.PushToken,
            "Payment Verified - On Waitlist",
            $"Your payment for {eventName} was verified. You're on the priority waitlist and will be added when a spot opens.",
            new { eventId = evt.Id.ToString(), type = "payment_verified_waitlist" },
            userId: user.Id,
            type: "payment_verified_waitlist",
            organizationId: evt.OrganizationId,
            eventId: evt.Id);
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

    // Organizer registration management
    public async Task<bool> RemoveRegistrationAsync(Guid eventId, Guid registrationId, Guid organizerId)
    {
        // Verify the organizer can manage this event
        var evt = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (evt == null) return false;

        if (!await CanUserManageEventAsync(evt, organizerId)) return false;

        var registration = await _context.EventRegistrations
            .Include(r => r.User)
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.EventId == eventId);

        if (registration == null) return false;

        // Can only remove registered or waitlisted users
        if (registration.Status != "Registered" && registration.Status != "Waitlisted")
        {
            return false;
        }

        var wasRegistered = registration.Status == "Registered";
        var wasWaitlisted = registration.Status == "Waitlisted";

        // Capture user and event for notification after transaction
        var removedUser = registration.User;
        var eventForNotification = registration.Event;

        // Use Serializable transaction to atomically remove and promote
        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        PromotionResult? promotionResult = null;

        try
        {
            // Cancel the registration
            registration.Status = "Cancelled";
            registration.WaitlistPosition = null;
            registration.PaymentDeadlineAt = null;

            await _context.SaveChangesAsync();

            // If a registered user was removed, promote next from waitlist
            if (wasRegistered)
            {
                promotionResult = await _waitlistService.PromoteFromWaitlistAsync(
                    eventId,
                    spotCount: 1,
                    callerOwnsTransaction: true  // We manage the transaction
                );
            }
            // If a waitlisted user was removed, renumber the waitlist
            else if (wasWaitlisted)
            {
                await _waitlistService.UpdateWaitlistPositionsAsync(eventId);
            }

            await transaction.CommitAsync();

            // Send notifications AFTER commit to prevent false notifications on rollback
            if (promotionResult != null)
            {
                await _waitlistService.SendPendingNotificationsAsync(
                    promotionResult.PendingNotifications);
            }

            // Notify the removed user
            await NotifyUserRemovedFromEventAsync(eventForNotification, removedUser);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Roster/Waitlist move operations (Phase 3 - Organizer Controls)
    public async Task<MoveResultDto> MoveToRosterAsync(Guid eventId, Guid registrationId, Guid organizerId)
    {
        var evt = await _context.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null)
            return new MoveResultDto(false, "Event not found", null);

        if (!await CanUserManageEventAsync(evt, organizerId))
            throw new UnauthorizedAccessException("You don't have permission to manage this event");

        var registration = await _context.EventRegistrations
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.EventId == eventId);

        if (registration == null)
            return new MoveResultDto(false, "Registration not found", null);

        if (registration.Status != "Waitlisted")
            return new MoveResultDto(false, "Player is not on the waitlist", null);

        // Check roster capacity (goalies don't count against MaxPlayers)
        var isGoalie = registration.RegisteredPosition == "Goalie";
        if (!isGoalie)
        {
            var skaterCount = evt.Registrations.Count(r => r.Status == "Registered" && r.RegisteredPosition != "Goalie");
            if (skaterCount >= evt.MaxPlayers)
                return new MoveResultDto(false, "Roster is full", null);
        }

        // Perform the move
        registration.Status = "Registered";
        registration.WaitlistPosition = null;
        registration.TeamAssignment = await DetermineTeamAssignmentAsync(eventId, registration.RegisteredPosition ?? "Skater");
        registration.PromotedAt = DateTime.UtcNow;

        // Set payment deadline if event has a cost
        if (evt.Cost > 0)
        {
            registration.PaymentDeadlineAt = DateTime.UtcNow.AddHours(2);
        }

        await _context.SaveChangesAsync();

        // Renumber remaining waitlist
        await _waitlistService.UpdateWaitlistPositionsAsync(eventId);

        // Build response DTO (no notification during draft - ShouldSendRosterNotification returns false)
        var dto = MapRegistrationToDto(registration);
        return new MoveResultDto(true, "Player moved to roster", dto);
    }

    public async Task<MoveResultDto> MoveToWaitlistAsync(Guid eventId, Guid registrationId, Guid organizerId)
    {
        var evt = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null)
            return new MoveResultDto(false, "Event not found", null);

        if (!await CanUserManageEventAsync(evt, organizerId))
            throw new UnauthorizedAccessException("You don't have permission to manage this event");

        var registration = await _context.EventRegistrations
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.EventId == eventId);

        if (registration == null)
            return new MoveResultDto(false, "Registration not found", null);

        if (registration.Status != "Registered")
            return new MoveResultDto(false, "Player is not on the roster", null);

        // Get next waitlist position
        var nextPosition = await _waitlistService.GetNextWaitlistPositionAsync(eventId);

        // Perform the move - clear roster-specific fields
        registration.Status = "Waitlisted";
        registration.WaitlistPosition = nextPosition;
        registration.TeamAssignment = null;      // No team on waitlist
        registration.RosterOrder = null;         // No roster order on waitlist
        registration.PromotedAt = null;          // Not promoted
        registration.PaymentDeadlineAt = null;   // No deadline on waitlist

        await _context.SaveChangesAsync();

        var dto = MapRegistrationToDto(registration);
        return new MoveResultDto(true, "Player moved to waitlist", dto);
    }

    public async Task<PublishResultDto> PublishRosterAsync(Guid eventId, Guid organizerId)
    {
        var evt = await _context.Events
            .Include(e => e.Registrations)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null)
            return new PublishResultDto(false, "Event not found", 0);

        if (!await CanUserManageEventAsync(evt, organizerId))
            throw new UnauthorizedAccessException("You don't have permission to manage this event");

        // Check if already published
        if (evt.IsRosterPublished)
            return new PublishResultDto(false, "Roster is already published", 0);

        // Mark as published
        evt.IsRosterPublished = true;
        evt.PublishedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Send notifications to all registered and waitlisted players
        var eventName = evt.Name ?? "Hockey Game";
        var sentCount = 0;

        foreach (var registration in evt.Registrations)
        {
            // Skip cancelled registrations
            if (registration.Status == "Cancelled")
                continue;

            // Skip users without push tokens
            if (string.IsNullOrEmpty(registration.User.PushToken))
                continue;

            string title;
            string body;

            if (registration.Status == "Registered")
            {
                title = "You're on the Roster!";
                if (!string.IsNullOrEmpty(registration.TeamAssignment))
                {
                    body = $"You're playing in {eventName}! Team {registration.TeamAssignment}.";
                }
                else
                {
                    body = $"You're playing in {eventName}! Check app for team assignment.";
                }
            }
            else if (registration.Status == "Waitlisted")
            {
                title = "You're on the Waitlist";
                body = $"You're #{registration.WaitlistPosition} on the waitlist for {eventName}.";
            }
            else
            {
                // Unknown status, skip
                continue;
            }

            await _notificationService.SendPushNotificationAsync(
                registration.User.PushToken,
                title,
                body,
                new { eventId = evt.Id.ToString(), type = "roster_published" },
                userId: registration.UserId,
                type: "roster_published",
                organizationId: evt.OrganizationId,
                eventId: evt.Id
            );

            sentCount++;
        }

        return new PublishResultDto(true, "Roster published successfully", sentCount);
    }

    /// <summary>
    /// Helper to map EventRegistration to EventRegistrationDto
    /// </summary>
    private EventRegistrationDto MapRegistrationToDto(EventRegistration registration)
    {
        return new EventRegistrationDto(
            registration.Id,
            registration.EventId,
            new UserDto(
                registration.User.Id,
                registration.User.Email,
                registration.User.FirstName,
                registration.User.LastName,
                registration.User.PhoneNumber,
                registration.User.Positions,
                registration.User.VenmoHandle,
                registration.User.Role,
                registration.User.CreatedAt,
                null,  // Badges not included in move response
                0,     // Total badge count not included
                registration.User.IsGhostPlayer
            ),
            registration.Status,
            registration.RegisteredAt,
            registration.RegisteredPosition,
            registration.PaymentStatus,
            registration.PaymentMarkedAt,
            registration.PaymentVerifiedAt,
            registration.TeamAssignment,
            registration.RosterOrder,
            registration.WaitlistPosition,
            registration.PromotedAt,
            registration.PaymentDeadlineAt,
            registration.IsWaitlisted
        );
    }

    // Notification helpers
    private async Task NotifyOrganizerNewWaitlistSignupAsync(Event evt, User user, int waitlistPosition)
    {
        // Don't notify about waitlist signups during draft mode
        if (!ShouldSendRosterNotification(evt))
        {
            return;
        }

        if (string.IsNullOrEmpty(evt.Creator.PushToken))
        {
            return;
        }

        var eventName = evt.Name ?? $"Event on {evt.EventDate:MMM d}";
        var userName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrEmpty(userName)) userName = user.Email;

        await _notificationService.SendPushNotificationAsync(
            evt.Creator.PushToken,
            "New Waitlist Signup",
            $"{userName} joined the waitlist (#{waitlistPosition}) for {eventName}",
            new { eventId = evt.Id.ToString(), type = "waitlist_signup" },
            userId: evt.CreatorId,
            type: "waitlist_signup",
            organizationId: evt.OrganizationId,
            eventId: evt.Id);
    }

    private async Task NotifyUserPromotedFromWaitlistAsync(Event evt, User user)
    {
        // Don't notify about promotions during draft mode
        if (!ShouldSendRosterNotification(evt))
        {
            return;
        }

        if (string.IsNullOrEmpty(user.PushToken))
        {
            return;
        }

        var eventName = evt.Name ?? $"Event on {evt.EventDate:MMM d}";

        await _notificationService.SendPushNotificationAsync(
            user.PushToken,
            "You're In!",
            $"You've been promoted from the waitlist for {eventName}.",
            new { eventId = evt.Id.ToString(), type = "promoted_from_waitlist" },
            userId: user.Id,
            type: "promoted_from_waitlist",
            organizationId: evt.OrganizationId,
            eventId: evt.Id);
    }

    /// <summary>
    /// Notify a user that they have been removed from an event by the organizer.
    /// </summary>
    private async Task NotifyUserRemovedFromEventAsync(Event evt, User user)
    {
        // Don't notify about removals during draft mode
        if (!ShouldSendRosterNotification(evt))
        {
            return;
        }

        if (string.IsNullOrEmpty(user.PushToken))
        {
            return;
        }

        var eventName = evt.Name ?? $"Event on {evt.EventDate:MMM d}";

        await _notificationService.SendPushNotificationAsync(
            user.PushToken,
            "Removed from Event",
            $"You have been removed from {eventName} by the organizer.",
            new { eventId = evt.Id.ToString(), type = "removed_from_event" },
            userId: user.Id,
            type: "removed_from_event",
            organizationId: evt.OrganizationId,
            eventId: evt.Id);
    }

    /// <summary>
    /// Batch loads badges for a list of users to prevent N+1 queries.
    /// Returns a dictionary mapping userId to (top 3 badges, total count).
    /// </summary>
    private async Task<Dictionary<Guid, (List<UserBadgeDto> TopBadges, int TotalCount)>> GetBadgesForUsersAsync(IEnumerable<Guid> userIds)
    {
        var userIdList = userIds.ToList();
        if (userIdList.Count == 0)
        {
            return new Dictionary<Guid, (List<UserBadgeDto>, int)>();
        }

        // Single query to get all badges for all users
        var allBadges = await _context.UserBadges
            .Include(ub => ub.BadgeType)
            .Where(ub => userIdList.Contains(ub.UserId))
            .ToListAsync();

        // Group by user and compute top 3 + total count
        var result = new Dictionary<Guid, (List<UserBadgeDto> TopBadges, int TotalCount)>();

        foreach (var userId in userIdList)
        {
            var userBadges = allBadges
                .Where(ub => ub.UserId == userId)
                .OrderBy(ub => ub.DisplayOrder ?? int.MaxValue)
                .ThenBy(ub => ub.BadgeType.SortPriority)
                .ToList();

            var topBadges = userBadges
                .Take(3)
                .Select(ub => new UserBadgeDto(
                    ub.Id,
                    new BadgeTypeDto(
                        ub.BadgeType.Id,
                        ub.BadgeType.Code,
                        ub.BadgeType.Name,
                        ub.BadgeType.Description,
                        ub.BadgeType.IconName,
                        ub.BadgeType.Category
                    ),
                    ub.Context,
                    ub.EarnedAt,
                    ub.DisplayOrder
                ))
                .ToList();

            result[userId] = (topBadges, userBadges.Count);
        }

        return result;
    }

    /// <summary>
    /// Search for users that can be added to an event's waitlist (organizer only).
    /// Returns users matching the query by first name or last name, excluding those already registered.
    /// </summary>
    public async Task<List<UserSearchResultDto>> SearchUsersForEventAsync(Guid eventId, Guid organizerId, string query)
    {
        // Verify organizer can manage this event
        if (!await CanUserManageEventAsync(eventId, organizerId))
        {
            throw new UnauthorizedAccessException("You don't have permission to manage this event");
        }

        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return new List<UserSearchResultDto>();
        }

        // Get user IDs already registered for this event (not cancelled)
        var registeredUserIds = await _context.EventRegistrations
            .Where(r => r.EventId == eventId && r.Status != "Cancelled")
            .Select(r => r.UserId)
            .ToListAsync();

        // Search users by first name OR last name (case-insensitive, contains)
        // Exclude ghost players - they are placeholders for non-app users
        var queryLower = query.ToLower();
        var users = await _context.Users
            .Where(u =>
                !u.IsGhostPlayer &&
                (u.FirstName.ToLower().Contains(queryLower) ||
                 u.LastName.ToLower().Contains(queryLower)) &&
                !registeredUserIds.Contains(u.Id))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Take(20)
            .Select(u => new UserSearchResultDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                Positions = u.Positions
            })
            .ToListAsync();

        return users;
    }

    /// <summary>
    /// Add a user to an event (organizer only).
    /// If roster has space, adds directly to roster. Otherwise, adds to waitlist.
    /// Sends appropriate notification to the user.
    /// </summary>
    public async Task<EventRegistrationDto> AddUserToWaitlistAsync(Guid eventId, Guid userId, Guid organizerId, string? position)
    {
        // Load event
        var evt = await _context.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null)
        {
            throw new InvalidOperationException("Event not found");
        }

        // Verify organizer can manage this event
        if (!await CanUserManageEventAsync(evt, organizerId))
        {
            throw new UnauthorizedAccessException("You don't have permission to manage this event");
        }

        // Verify user exists
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Check if user has any existing registration (including cancelled)
        var existingReg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId);

        // If already registered or waitlisted, throw error
        if (existingReg != null && (existingReg.Status == "Registered" || existingReg.Status == "Waitlisted"))
        {
            throw new InvalidOperationException("User is already registered for this event");
        }

        // Determine position and check roster capacity (goalies don't count against MaxPlayers)
        var registeredPosition = NormalizePosition(position);
        var isPaidEvent = evt.Cost > 0;
        var isGoalie = registeredPosition == "Goalie";
        var skaterCount = evt.Registrations.Count(r => r.Status == "Registered" && r.RegisteredPosition != "Goalie");
        var hasRosterSpace = isGoalie || skaterCount < evt.MaxPlayers;

        // Get or create registration
        EventRegistration registration;
        if (existingReg != null)
        {
            // Re-activate cancelled registration
            registration = existingReg;
            registration.RegisteredAt = DateTime.UtcNow;
            registration.RegisteredPosition = registeredPosition;
            registration.PaymentMarkedAt = null;
            registration.PaymentVerifiedAt = null;
        }
        else
        {
            // Create new registration
            registration = new EventRegistration
            {
                EventId = eventId,
                UserId = userId,
                RegisteredPosition = registeredPosition
            };
            _context.EventRegistrations.Add(registration);
        }

        // Set roster or waitlist properties
        if (hasRosterSpace)
        {
            registration.Status = "Registered";
            registration.WaitlistPosition = null;
            registration.TeamAssignment = await DetermineTeamAssignmentAsync(eventId, registeredPosition);
            registration.PromotedAt = DateTime.UtcNow;
            registration.PaymentStatus = isPaidEvent ? "Pending" : null;
            registration.PaymentDeadlineAt = isPaidEvent ? DateTime.UtcNow.AddHours(2) : null;
        }
        else
        {
            registration.Status = "Waitlisted";
            registration.WaitlistPosition = await _waitlistService.GetNextWaitlistPositionAsync(eventId);
            registration.TeamAssignment = null;
            registration.RosterOrder = null;
            registration.PromotedAt = null;
            registration.PaymentStatus = isPaidEvent ? "Pending" : null;
            registration.PaymentDeadlineAt = null;
        }

        await _context.SaveChangesAsync();

        // Send appropriate notification to user (only if roster is published)
        if (evt.IsRosterPublished)
        {
            if (registration.Status == "Registered")
            {
                await NotifyUserAddedToRosterAsync(evt, user, registration.TeamAssignment);
            }
            else
            {
                await NotifyUserAddedToWaitlistAsync(evt, user, registration.WaitlistPosition ?? 1);
            }
        }

        // Load user for DTO
        await _context.Entry(registration).Reference(r => r.User).LoadAsync();

        return MapRegistrationToDto(registration);
    }

    /// <summary>
    /// Normalizes and validates a position string.
    /// Organizers can add users as any position, regardless of the user's profile.
    /// </summary>
    private static string NormalizePosition(string? position)
    {
        if (string.IsNullOrEmpty(position))
        {
            return "Skater";
        }

        var normalized = position.ToLowerInvariant();
        if (normalized != "goalie" && normalized != "skater")
        {
            throw new InvalidOperationException("Invalid position. Must be 'Goalie' or 'Skater'");
        }

        return normalized == "goalie" ? "Goalie" : "Skater";
    }

    /// <summary>
    /// Notify a user that they have been added to an event's waitlist by an organizer.
    /// </summary>
    private async Task NotifyUserAddedToWaitlistAsync(Models.Entities.Event evt, Models.Entities.User user, int waitlistPosition)
    {
        if (string.IsNullOrEmpty(user.PushToken))
        {
            return;
        }

        var eventName = evt.Name ?? $"Event on {evt.EventDate:MMM d}";

        await _notificationService.SendPushNotificationAsync(
            user.PushToken,
            "Added to Waitlist",
            $"You've been added to the waitlist for {eventName} (#{waitlistPosition}).",
            new { eventId = evt.Id.ToString(), type = "added_to_waitlist" },
            userId: user.Id,
            type: "added_to_waitlist",
            organizationId: evt.OrganizationId,
            eventId: evt.Id);
    }

    /// <summary>
    /// Create a ghost player and add them to an event's waitlist (organizer only).
    /// Ghost players are placeholder accounts for people who don't have the app.
    /// </summary>
    public async Task<EventRegistrationDto> CreateGhostPlayerAsync(
        Guid eventId,
        Guid organizerId,
        string firstName,
        string lastName,
        string position,
        string? skillLevel)
    {
        // Load event
        var evt = await _context.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null)
        {
            throw new InvalidOperationException("Event not found");
        }

        // Verify organizer can manage this event
        if (!await CanUserManageEventAsync(evt, organizerId))
        {
            throw new UnauthorizedAccessException("You don't have permission to manage this event");
        }

        // Validate position and skill level
        var registeredPosition = NormalizePosition(position);
        var normalizedPositionKey = position.ToLowerInvariant();
        if (!string.IsNullOrEmpty(skillLevel) && !ValidSkillLevels.Contains(skillLevel))
        {
            throw new InvalidOperationException($"Invalid skill level: '{skillLevel}'. Valid values: Gold, Silver, Bronze, D-League");
        }

        // Create ghost user (cannot log in, placeholder for non-app users)
        var ghostUser = new User
        {
            Id = Guid.NewGuid(),
            Email = $"ghost_{Guid.NewGuid():N}@placeholder.bhmhockey",
            PasswordHash = "",
            FirstName = firstName,
            LastName = lastName,
            IsGhostPlayer = true,
            IsActive = true,
            Role = "Player",
            Positions = new Dictionary<string, string>
            {
                { normalizedPositionKey, skillLevel ?? "Bronze" }
            }
        };

        _context.Users.Add(ghostUser);
        await _context.SaveChangesAsync();

        // Check roster capacity and create registration (goalies don't count against MaxPlayers)
        var isGoalie = registeredPosition == "Goalie";
        var skaterCount = evt.Registrations.Count(r => r.Status == "Registered" && r.RegisteredPosition != "Goalie");
        var hasRosterSpace = isGoalie || skaterCount < evt.MaxPlayers;
        var isPaidEvent = evt.Cost > 0;

        // Create registration - ghost players are auto-verified for payment
        var registration = new EventRegistration
        {
            EventId = eventId,
            UserId = ghostUser.Id,
            RegisteredPosition = registeredPosition,
            PaymentStatus = isPaidEvent ? "Verified" : null
        };

        if (hasRosterSpace)
        {
            registration.Status = "Registered";
            registration.TeamAssignment = await DetermineTeamAssignmentAsync(eventId, registeredPosition);
            registration.PromotedAt = DateTime.UtcNow;
        }
        else
        {
            registration.Status = "Waitlisted";
            registration.WaitlistPosition = await _waitlistService.GetNextWaitlistPositionAsync(eventId);
        }

        _context.EventRegistrations.Add(registration);
        await _context.SaveChangesAsync();

        // Load user for DTO
        registration.User = ghostUser;

        return MapRegistrationToDto(registration);
    }

    /// <summary>
    /// Notify a user that they have been added directly to an event's roster by an organizer.
    /// </summary>
    private async Task NotifyUserAddedToRosterAsync(Models.Entities.Event evt, Models.Entities.User user, string? teamAssignment)
    {
        if (string.IsNullOrEmpty(user.PushToken))
        {
            return;
        }

        var eventName = evt.Name ?? $"Event on {evt.EventDate:MMM d}";
        var teamInfo = !string.IsNullOrEmpty(teamAssignment) ? $" on Team {teamAssignment}" : "";

        await _notificationService.SendPushNotificationAsync(
            user.PushToken,
            "Added to Roster",
            $"You've been added to the roster{teamInfo} for {eventName}.",
            new { eventId = evt.Id.ToString(), type = "added_to_roster" },
            userId: user.Id,
            type: "added_to_roster",
            organizationId: evt.OrganizationId,
            eventId: evt.Id);
    }
}
