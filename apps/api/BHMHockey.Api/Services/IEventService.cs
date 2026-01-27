using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface IEventService
{
    Task<EventDto> CreateAsync(CreateEventRequest request, Guid creatorId);
    Task<List<EventDto>> GetAllAsync(Guid? currentUserId = null);
    Task<List<EventDto>> GetByOrganizationAsync(Guid organizationId, Guid? currentUserId = null);
    Task<EventDto?> GetByIdAsync(Guid id, Guid? currentUserId = null);
    Task<EventDto?> UpdateAsync(Guid id, UpdateEventRequest request, Guid userId);
    Task<bool> DeleteAsync(Guid id, Guid userId);
    Task<RegistrationResultDto> RegisterAsync(Guid eventId, Guid userId, string? position = null);
    Task<bool> CancelRegistrationAsync(Guid eventId, Guid userId);
    Task<List<EventRegistrationDto>> GetRegistrationsAsync(Guid eventId);
    Task<List<EventDto>> GetUserRegistrationsAsync(Guid userId);

    // Payment methods (Phase 4)
    Task<bool> MarkPaymentAsync(Guid eventId, Guid userId, string? paymentReference);
    Task<PaymentUpdateResultDto> UpdatePaymentStatusAsync(Guid eventId, Guid registrationId, string paymentStatus, Guid organizerId);

    // Team assignment methods
    Task<bool> UpdateTeamAssignmentAsync(Guid eventId, Guid registrationId, string teamAssignment, Guid organizerId);

    // Roster order methods
    Task<bool> UpdateRosterOrderAsync(Guid eventId, List<RosterOrderItem> items, Guid organizerId);

    // Organizer registration management
    Task<bool> RemoveRegistrationAsync(Guid eventId, Guid registrationId, Guid organizerId);

    /// <summary>
    /// Move a waitlisted player to the roster (organizer only).
    /// Returns failure if roster is full or player is not waitlisted.
    /// </summary>
    Task<MoveResultDto> MoveToRosterAsync(Guid eventId, Guid registrationId, Guid organizerId);

    /// <summary>
    /// Move a rostered player to the waitlist (organizer only).
    /// Clears team assignment and payment deadline, assigns next waitlist position.
    /// </summary>
    Task<MoveResultDto> MoveToWaitlistAsync(Guid eventId, Guid registrationId, Guid organizerId);

    // Authorization helpers
    Task<bool> CanUserManageEventAsync(Guid eventId, Guid userId);

    // Waitlist with badges (for organizer view)
    Task<List<EventRegistrationDto>> GetWaitlistWithBadgesAsync(Guid eventId);

    /// <summary>
    /// Publish the roster for an event (organizer only).
    /// Sets IsRosterPublished=true, records PublishedAt timestamp, and sends notifications to all players.
    /// Returns failure if roster is already published.
    /// </summary>
    Task<PublishResultDto> PublishRosterAsync(Guid eventId, Guid organizerId);

    /// <summary>
    /// Search for users that can be added to an event's waitlist (organizer only).
    /// Returns users matching the query by first name or last name, excluding those already registered.
    /// </summary>
    Task<List<UserSearchResultDto>> SearchUsersForEventAsync(Guid eventId, Guid organizerId, string query);

    /// <summary>
    /// Add a user to an event's waitlist (organizer only).
    /// Creates a new registration with Status="Waitlisted" and sends a notification to the user.
    /// </summary>
    Task<EventRegistrationDto> AddUserToWaitlistAsync(Guid eventId, Guid userId, Guid organizerId, string? position);
}
