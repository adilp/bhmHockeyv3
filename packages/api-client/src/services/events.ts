import type { Event, EventRegistration } from '@bhmhockey/shared';
import { apiClient } from '../client';

/**
 * Event service
 */
export const eventService = {
  /**
   * Get all events
   */
  async getAll(): Promise<Event[]> {
    const response = await apiClient.instance.get<Event[]>('/events');
    return response.data;
  },

  /**
   * Get event by ID
   */
  async getById(id: string): Promise<Event> {
    const response = await apiClient.instance.get<Event>(`/events/${id}`);
    return response.data;
  },

  /**
   * Create event
   */
  async create(data: Partial<Event>): Promise<Event> {
    const response = await apiClient.instance.post<Event>('/events', data);
    return response.data;
  },

  /**
   * Update event
   */
  async update(id: string, data: Partial<Event>): Promise<Event> {
    const response = await apiClient.instance.put<Event>(`/events/${id}`, data);
    return response.data;
  },

  /**
   * Cancel event
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
   * Get event registrations
   */
  async getRegistrations(eventId: string): Promise<EventRegistration[]> {
    const response = await apiClient.instance.get<EventRegistration[]>(`/events/${eventId}/registrations`);
    return response.data;
  },

  /**
   * Get user's registrations
   */
  async getMyRegistrations(): Promise<EventRegistration[]> {
    const response = await apiClient.instance.get<EventRegistration[]>('/users/me/registrations');
    return response.data;
  },
};
