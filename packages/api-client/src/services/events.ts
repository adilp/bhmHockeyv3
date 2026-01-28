import type {
  EventDto,
  EventRegistrationDto,
  CreateEventRequest,
  UpdateEventRequest,
  MarkPaymentRequest,
  UpdatePaymentStatusRequest,
  PaymentUpdateResultDto,
  MoveResultDto,
  PublishResultDto,
  UpdateTeamAssignmentRequest,
  UpdateRosterOrderRequest,
  RosterOrderItem,
  RegisterForEventRequest,
  RegistrationResultDto,
  Position,
  TeamAssignment,
  WaitlistOrderItem,
  UserSearchResultDto,
  AddUserToEventRequest,
  CreateGhostPlayerRequest
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
   * @param eventId Event ID to register for
   * @param position Optional position (required if user has multiple positions in profile)
   * @returns Registration result with status (Registered or Waitlisted) and waitlist position if applicable
   */
  async register(eventId: string, position?: Position): Promise<RegistrationResultDto> {
    const body: RegisterForEventRequest = position ? { position } : {};
    const response = await apiClient.instance.post<RegistrationResultDto>(`/events/${eventId}/register`, body);
    return response.data;
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
   * Returns detailed result including whether user was promoted to roster
   */
  async updatePaymentStatus(
    eventId: string,
    registrationId: string,
    request: UpdatePaymentStatusRequest
  ): Promise<PaymentUpdateResultDto> {
    const response = await apiClient.instance.put<PaymentUpdateResultDto>(
      `/events/${eventId}/registrations/${registrationId}/payment`,
      request
    );
    return response.data;
  },

  // Team assignment methods

  /**
   * Update team assignment for a registration (organizer only)
   */
  async updateTeamAssignment(
    eventId: string,
    registrationId: string,
    teamAssignment: TeamAssignment
  ): Promise<void> {
    await apiClient.instance.put(
      `/events/${eventId}/registrations/${registrationId}/team`,
      { teamAssignment }
    );
  },

  // Registration management (organizer)

  /**
   * Remove a registration (organizer only)
   * Works for both registered and waitlisted users
   */
  async removeRegistration(eventId: string, registrationId: string): Promise<void> {
    await apiClient.instance.delete(
      `/events/${eventId}/registrations/${registrationId}`
    );
  },

  // Roster/Waitlist move operations (organizer)

  /**
   * Move a waitlisted player to the roster (organizer only)
   * Returns error if roster is full
   */
  async moveToRoster(eventId: string, registrationId: string): Promise<MoveResultDto> {
    const response = await apiClient.instance.post<MoveResultDto>(
      `/events/${eventId}/registrations/${registrationId}/move-to-roster`
    );
    return response.data;
  },

  /**
   * Move a rostered player to the waitlist (organizer only)
   */
  async moveToWaitlist(eventId: string, registrationId: string): Promise<MoveResultDto> {
    const response = await apiClient.instance.post<MoveResultDto>(
      `/events/${eventId}/registrations/${registrationId}/move-to-waitlist`
    );
    return response.data;
  },

  // Roster publishing (organizer)

  /**
   * Publish the roster for an event (organizer only).
   * Sets isRosterPublished=true and notifies all players of their placement.
   */
  async publishRoster(eventId: string): Promise<PublishResultDto> {
    const response = await apiClient.instance.post<PublishResultDto>(
      `/events/${eventId}/publish-roster`
    );
    return response.data;
  },

  // Roster order management (organizer)

  /**
   * Update roster order for all registrations (organizer only)
   * Allows reordering players and changing teams in a single batch update
   */
  async updateRosterOrder(eventId: string, items: RosterOrderItem[]): Promise<void> {
    await apiClient.instance.put(
      `/events/${eventId}/roster-order`,
      { items }
    );
  },

  // Waitlist management (organizer)

  /**
   * Reorder waitlist positions (organizer only)
   * All waitlisted users must be included with sequential positions starting from 1
   */
  async reorderWaitlist(eventId: string, items: WaitlistOrderItem[]): Promise<void> {
    await apiClient.instance.put(
      `/events/${eventId}/waitlist/reorder`,
      { items }
    );
  },

  // User management (organizer)

  /**
   * Search for users that can be added to an event's waitlist (organizer only)
   * Returns users matching the query by first name or last name, excluding those already registered
   */
  async searchUsersForEvent(eventId: string, query: string): Promise<UserSearchResultDto[]> {
    const response = await apiClient.instance.get<UserSearchResultDto[]>(
      `/events/${eventId}/search-users`,
      { params: { query } }
    );
    return response.data;
  },

  /**
   * Add a user to an event's waitlist (organizer only)
   * Creates a new registration with Status="Waitlisted" and sends a notification to the user
   */
  async addUserToEvent(eventId: string, request: AddUserToEventRequest): Promise<EventRegistrationDto> {
    const response = await apiClient.instance.post<EventRegistrationDto>(
      `/events/${eventId}/registrations/add-user`,
      request
    );
    return response.data;
  },

  /**
   * Create a ghost player and add them to an event's waitlist (organizer only).
   * Ghost players are placeholder accounts for people who don't have the app.
   */
  async createGhostPlayer(eventId: string, request: CreateGhostPlayerRequest): Promise<EventRegistrationDto> {
    const response = await apiClient.instance.post<EventRegistrationDto>(
      `/events/${eventId}/registrations/create-ghost-player`,
      request
    );
    return response.data;
  },
};
