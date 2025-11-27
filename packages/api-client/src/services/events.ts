import type {
  EventDto,
  EventRegistrationDto,
  CreateEventRequest,
  UpdateEventRequest,
  MarkPaymentRequest,
  UpdatePaymentStatusRequest
} from '@bhmhockey/shared';
import { apiClient } from '../client';

/**
 * Event service - uses EventDto which includes computed fields (registeredCount, isRegistered, organizationName)
 */
export const eventService = {
  /**
   * Get all upcoming events
   */
  async getAll(organizationId?: string): Promise<EventDto[]> {
    const params = organizationId ? { organizationId } : undefined;
    const response = await apiClient.instance.get<EventDto[]>('/events', { params });
    return response.data;
  },

  /**
   * Get event by ID
   */
  async getById(id: string): Promise<EventDto> {
    const response = await apiClient.instance.get<EventDto>(`/events/${id}`);
    return response.data;
  },

  /**
   * Create event
   */
  async create(data: CreateEventRequest): Promise<EventDto> {
    const response = await apiClient.instance.post<EventDto>('/events', data);
    return response.data;
  },

  /**
   * Update event
   */
  async update(id: string, data: UpdateEventRequest): Promise<EventDto> {
    const response = await apiClient.instance.put<EventDto>(`/events/${id}`, data);
    return response.data;
  },

  /**
   * Cancel/delete event
   */
  async cancel(id: string): Promise<void> {
    await apiClient.instance.delete(`/events/${id}`);
  },

  /**
   * Register for event
   */
  async register(eventId: string): Promise<void> {
    await apiClient.instance.post(`/events/${eventId}/register`);
  },

  /**
   * Cancel registration
   */
  async cancelRegistration(eventId: string): Promise<void> {
    await apiClient.instance.delete(`/events/${eventId}/register`);
  },

  /**
   * Get event registrations (attendee list)
   */
  async getRegistrations(eventId: string): Promise<EventRegistrationDto[]> {
    const response = await apiClient.instance.get<EventRegistrationDto[]>(`/events/${eventId}/registrations`);
    return response.data;
  },

  /**
   * Get current user's registered events
   */
  async getMyRegistrations(): Promise<EventDto[]> {
    const response = await apiClient.instance.get<EventDto[]>('/users/me/registrations');
    return response.data;
  },

  // Payment methods (Phase 4)

  /**
   * Mark payment as complete
   */
  async markPayment(eventId: string, request?: MarkPaymentRequest): Promise<void> {
    await apiClient.instance.post(`/events/${eventId}/payment/mark-paid`, request || {});
  },

  /**
   * Update payment status (organizer only)
   */
  async updatePaymentStatus(
    eventId: string,
    registrationId: string,
    request: UpdatePaymentStatusRequest
  ): Promise<void> {
    await apiClient.instance.put(
      `/events/${eventId}/registrations/${registrationId}/payment`,
      request
    );
  },
};
