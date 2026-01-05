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
    Task<bool> UpdatePaymentStatusAsync(Guid eventId, Guid registrationId, string paymentStatus, Guid organizerId);

    // Team assignment methods
    Task<bool> UpdateTeamAssignmentAsync(Guid eventId, Guid registrationId, string teamAssignment, Guid organizerId);

    // Roster order methods
    Task<bool> UpdateRosterOrderAsync(Guid eventId, List<RosterOrderItem> items, Guid organizerId);

    // Organizer registration management
    Task<bool> RemoveRegistrationAsync(Guid eventId, Guid registrationId, Guid organizerId);

    // Authorization helpers
    Task<bool> CanUserManageEventAsync(Guid eventId, Guid userId);
}
