using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface IOrganizationService
{
    Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, Guid creatorId);
    Task<List<OrganizationDto>> GetAllAsync(Guid? currentUserId = null);
    Task<OrganizationDto?> GetByIdAsync(Guid id, Guid? currentUserId = null);
    Task<OrganizationDto?> UpdateAsync(Guid id, UpdateOrganizationRequest request, Guid userId);
    Task<bool> DeleteAsync(Guid id, Guid userId);
    Task<bool> SubscribeAsync(Guid organizationId, Guid userId);
    Task<bool> UnsubscribeAsync(Guid organizationId, Guid userId);
    Task<List<OrganizationSubscriptionDto>> GetUserSubscriptionsAsync(Guid userId);
    Task<List<OrganizationDto>> GetUserAdminOrganizationsAsync(Guid userId);
    Task<List<OrganizationMemberDto>> GetMembersAsync(Guid organizationId, Guid requesterId);
    Task<bool> RemoveMemberAsync(Guid organizationId, Guid memberUserId, Guid requesterId);
}
