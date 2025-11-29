import type {
  Organization,
  OrganizationSubscription,
  OrganizationMember,
  CreateOrganizationRequest,
  UpdateOrganizationRequest
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
};
