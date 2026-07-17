import type {
  Organization,
  OrganizationSubscription,
  OrganizationMember,
  OrganizationAdmin,
  CreateOrganizationRequest,
  UpdateOrganizationRequest,
  AddAdminRequest,
  AutoRosterMember,
  AddAutoRosterMemberRequest
} from '@bhmhockey/shared';
import { apiClient } from '../client';

/**
 * Organization service
 */
export const organizationService = {
  /**
   * Get all organizations
   */
  async getAll(): Promise<Organization[]> {
    const response = await apiClient.instance.get<Organization[]>('/organizations');
    return response.data;
  },

  /**
   * Get organization by ID
   */
  async getById(id: string): Promise<Organization> {
    const response = await apiClient.instance.get<Organization>(`/organizations/${id}`);
    return response.data;
  },

  /**
   * Create organization
   */
  async create(data: CreateOrganizationRequest): Promise<Organization> {
    const response = await apiClient.instance.post<Organization>('/organizations', data);
    return response.data;
  },

  /**
   * Update organization
   */
  async update(id: string, data: UpdateOrganizationRequest): Promise<Organization> {
    const response = await apiClient.instance.put<Organization>(`/organizations/${id}`, data);
    return response.data;
  },

  /**
   * Delete organization (soft delete - sets IsActive to false)
   */
  async delete(id: string): Promise<void> {
    await apiClient.instance.delete(`/organizations/${id}`);
  },

  /**
   * Subscribe to organization
   */
  async subscribe(organizationId: string): Promise<void> {
    await apiClient.instance.post(`/organizations/${organizationId}/subscribe`);
  },

  /**
   * Unsubscribe from organization
   */
  async unsubscribe(organizationId: string): Promise<void> {
    await apiClient.instance.delete(`/organizations/${organizationId}/subscribe`);
  },

  /**
   * Get user's subscriptions
   */
  async getMySubscriptions(): Promise<OrganizationSubscription[]> {
    const response = await apiClient.instance.get<OrganizationSubscription[]>('/users/me/subscriptions');
    return response.data;
  },

  /**
   * Get organizations created by the current user
   */
  async getMyOrganizations(): Promise<Organization[]> {
    const response = await apiClient.instance.get<Organization[]>('/users/me/organizations');
    return response.data;
  },

  /**
   * Get members of an organization (admin only)
   */
  async getMembers(organizationId: string): Promise<OrganizationMember[]> {
    const response = await apiClient.instance.get<OrganizationMember[]>(`/organizations/${organizationId}/members`);
    return response.data;
  },

  /**
   * Remove a member from an organization (admin only)
   */
  async removeMember(organizationId: string, userId: string): Promise<void> {
    await apiClient.instance.delete(`/organizations/${organizationId}/members/${userId}`);
  },

  // Admin management methods

  /**
   * Get all admins of an organization (admin only)
   */
  async getAdmins(organizationId: string): Promise<OrganizationAdmin[]> {
    const response = await apiClient.instance.get<OrganizationAdmin[]>(`/organizations/${organizationId}/admins`);
    return response.data;
  },

  /**
   * Add an admin to an organization (admin only)
   */
  async addAdmin(organizationId: string, data: AddAdminRequest): Promise<void> {
    await apiClient.instance.post(`/organizations/${organizationId}/admins`, data);
  },

  /**
   * Remove an admin from an organization (admin only)
   */
  async removeAdmin(organizationId: string, userId: string): Promise<void> {
    await apiClient.instance.delete(`/organizations/${organizationId}/admins/${userId}`);
  },

  // Auto-roster methods (admin only) - org "regulars" auto-added to new org events

  /**
   * Get the organization's auto-roster list ordered by sort order (admin only)
   */
  async getAutoRoster(organizationId: string): Promise<AutoRosterMember[]> {
    const response = await apiClient.instance.get<AutoRosterMember[]>(`/organizations/${organizationId}/auto-roster`);
    return response.data;
  },

  /**
   * Add a subscriber to the organization's auto-roster (admin only)
   */
  async addAutoRosterMember(organizationId: string, data: AddAutoRosterMemberRequest): Promise<AutoRosterMember> {
    const response = await apiClient.instance.post<AutoRosterMember>(`/organizations/${organizationId}/auto-roster`, data);
    return response.data;
  },

  /**
   * Remove a user from the organization's auto-roster (admin only)
   */
  async removeAutoRosterMember(organizationId: string, userId: string): Promise<void> {
    await apiClient.instance.delete(`/organizations/${organizationId}/auto-roster/${userId}`);
  },

  /**
   * Reorder the organization's auto-roster (admin only).
   * All current members must be included exactly once.
   */
  async reorderAutoRoster(organizationId: string, orderedUserIds: string[]): Promise<AutoRosterMember[]> {
    const response = await apiClient.instance.put<AutoRosterMember[]>(
      `/organizations/${organizationId}/auto-roster/order`,
      { orderedUserIds }
    );
    return response.data;
  },
};
