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
    Task<bool> RegisterAsync(Guid eventId, Guid userId);
    Task<bool> CancelRegistrationAsync(Guid eventId, Guid userId);
    Task<List<EventRegistrationDto>> GetRegistrationsAsync(Guid eventId);
    Task<List<EventDto>> GetUserRegistrationsAsync(Guid userId);
}
