import { create } from 'zustand';
import { eventService } from '@bhmhockey/api-client';
import type { EventDto, CreateEventRequest, Position, TeamAssignment, RegistrationResultDto } from '@bhmhockey/shared';

interface EventState {
  // State
  events: EventDto[];
  myRegistrations: EventDto[];
  selectedEvent: EventDto | null;
  isLoading: boolean;
  isCreating: boolean;
  processingEventId: string | null; // Track which event is being registered/cancelled
  error: string | null;

  // Actions
  fetchEvents: (organizationId?: string) => Promise<void>;
  fetchEventById: (id: string) => Promise<void>;
  fetchMyRegistrations: () => Promise<void>;
  createEvent: (data: CreateEventRequest) => Promise<EventDto | null>;
  register: (eventId: string, position?: Position) => Promise<RegistrationResultDto | null>;
  cancelRegistration: (eventId: string) => Promise<boolean>;
  clearSelectedEvent: () => void;
  clearError: () => void;

  // Payment actions (Phase 4)
  markPayment: (eventId: string) => Promise<boolean>;
  updatePaymentStatus: (eventId: string, registrationId: string, status: 'Verified' | 'Pending') => Promise<boolean>;

  // Team assignment actions
  updateTeamAssignment: (eventId: string, registrationId: string, team: TeamAssignment) => Promise<boolean>;

  // Registration management (organizer)
  removeRegistration: (eventId: string, registrationId: string) => Promise<boolean>;

  // Event management (organizer)
  cancelEvent: (eventId: string) => Promise<boolean>;
}

export const useEventStore = create<EventState>((set, get) => ({
  // Initial state
  events: [],
  myRegistrations: [],
  selectedEvent: null,
  isLoading: false,
  isCreating: false,
  processingEventId: null,
  error: null,

  // Fetch all upcoming events (optionally filtered by organization)
  fetchEvents: async (organizationId?: string) => {
    set({ isLoading: true, error: null });
    try {
      const events = await eventService.getAll(organizationId);
      // Sort by event date ascending (earliest first)
      const sortedEvents = events.sort(
        (a, b) => new Date(a.eventDate).getTime() - new Date(b.eventDate).getTime()
      );
      set({ events: sortedEvents, isLoading: false });
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to load events',
        isLoading: false
      });
    }
  },

  // Fetch single event by ID
  fetchEventById: async (id: string) => {
    set({ isLoading: true, error: null });
    try {
      const event = await eventService.getById(id);
      set({ selectedEvent: event, isLoading: false });
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to load event',
        isLoading: false
      });
    }
  },

  // Fetch user's registered events
  fetchMyRegistrations: async () => {
    try {
      const myRegistrations = await eventService.getMyRegistrations();
      // Sort by event date ascending (earliest first)
      const sortedRegistrations = myRegistrations.sort(
        (a, b) => new Date(a.eventDate).getTime() - new Date(b.eventDate).getTime()
      );
      set({ myRegistrations: sortedRegistrations });
    } catch (error) {
      // Silently fail - user might not be logged in
      console.log('Failed to fetch registrations:', error);
    }
  },

  // Create a new event
  createEvent: async (data: CreateEventRequest) => {
    set({ isCreating: true, error: null });
    try {
      const newEvent = await eventService.create(data);
      const { events } = get();
      // Add new event to list, sorted by date
      const updatedEvents = [...events, newEvent].sort(
        (a, b) => new Date(a.eventDate).getTime() - new Date(b.eventDate).getTime()
      );
      set({ events: updatedEvents, isCreating: false });
      return newEvent;
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to create event',
        isCreating: false
      });
      return null;
    }
  },

  // Register for event (with optimistic update)
  register: async (eventId: string, position?: Position): Promise<RegistrationResultDto | null> => {
    const { events, selectedEvent, myRegistrations } = get();
    const targetEvent = events.find(e => e.id === eventId) || selectedEvent;

    // Check if there's room for optimistic update
    const hasRoom = targetEvent ? targetEvent.registeredCount < targetEvent.maxPlayers : false;

    // Helper to update event for registered status
    const updateEventAsRegistered = (event: EventDto): EventDto => ({
      ...event,
      isRegistered: true,
      registeredCount: event.registeredCount + 1,
      myPaymentStatus: event.cost > 0 ? 'Pending' : undefined,
      amIWaitlisted: false,
      myWaitlistPosition: undefined,
    });

    // Helper to update event for waitlisted status
    const updateEventAsWaitlisted = (event: EventDto, waitlistPosition: number): EventDto => ({
      ...event,
      isRegistered: false,
      amIWaitlisted: true,
      myWaitlistPosition: waitlistPosition,
      waitlistCount: event.waitlistCount + 1,
    });

    // Only do optimistic update if there's room
    if (hasRoom) {
      set({
        processingEventId: eventId,
        events: events.map(e => e.id === eventId ? updateEventAsRegistered(e) : e),
        selectedEvent: selectedEvent?.id === eventId ? updateEventAsRegistered(selectedEvent) : selectedEvent,
      });
    } else {
      // Just show loading, no optimistic update for potential waitlist
      set({ processingEventId: eventId });
    }

    try {
      const result = await eventService.register(eventId, position);

      // Update state based on actual result
      if (result.status === 'Registered') {
        const registeredEvent = events.find(e => e.id === eventId);
        if (registeredEvent) {
          const updatedEvent = updateEventAsRegistered(registeredEvent);
          set({
            events: events.map(e => e.id === eventId ? updatedEvent : e),
            selectedEvent: selectedEvent?.id === eventId ? updateEventAsRegistered(selectedEvent) : selectedEvent,
            myRegistrations: [...myRegistrations, updatedEvent],
            processingEventId: null,
          });
        } else {
          set({ processingEventId: null });
        }
      } else if (result.status === 'Waitlisted') {
        // User was added to waitlist
        const waitlistPosition = result.waitlistPosition ?? 1;
        set({
          events: events.map(e => e.id === eventId ? updateEventAsWaitlisted(e, waitlistPosition) : e),
          selectedEvent: selectedEvent?.id === eventId ? updateEventAsWaitlisted(selectedEvent, waitlistPosition) : selectedEvent,
          processingEventId: null,
        });
      }

      return result;
    } catch (error: any) {
      // Rollback on failure
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to register';
      set({
        events,
        selectedEvent,
        processingEventId: null,
        error: errorMessage
      });
      return null;
    }
  },

  // Cancel registration (with optimistic update)
  cancelRegistration: async (eventId: string) => {
    const { events, selectedEvent, myRegistrations } = get();
    const targetEvent = events.find(e => e.id === eventId) || selectedEvent;
    const wasWaitlisted = targetEvent?.amIWaitlisted ?? false;

    // Helper to update event when cancelling a registered user
    const updateEventCancelRegistered = (event: EventDto): EventDto => ({
      ...event,
      isRegistered: false,
      registeredCount: Math.max(0, event.registeredCount - 1),
      myPaymentStatus: undefined,
      myTeamAssignment: undefined,
    });

    // Helper to update event when cancelling a waitlisted user
    const updateEventCancelWaitlisted = (event: EventDto): EventDto => ({
      ...event,
      amIWaitlisted: false,
      myWaitlistPosition: undefined,
      waitlistCount: Math.max(0, event.waitlistCount - 1),
    });

    // Choose update function based on current status
    const updateEvent = wasWaitlisted ? updateEventCancelWaitlisted : updateEventCancelRegistered;

    // Optimistic update + track processing
    set({
      processingEventId: eventId,
      events: events.map(e => e.id === eventId ? updateEvent(e) : e),
      selectedEvent: selectedEvent?.id === eventId ? updateEvent(selectedEvent) : selectedEvent,
      myRegistrations: wasWaitlisted ? myRegistrations : myRegistrations.filter(e => e.id !== eventId),
    });

    try {
      await eventService.cancelRegistration(eventId);
      set({ processingEventId: null });
      return true;
    } catch (error) {
      // Rollback on failure
      set({
        events,
        selectedEvent,
        myRegistrations,
        processingEventId: null,
        error: error instanceof Error ? error.message : 'Failed to cancel registration'
      });
      return false;
    }
  },

  clearSelectedEvent: () => set({ selectedEvent: null }),
  clearError: () => set({ error: null }),

  // Payment actions (Phase 4)

  // Mark payment as complete
  markPayment: async (eventId: string) => {
    const { events, selectedEvent, myRegistrations } = get();

    // Optimistic update
    const updateEvent = (event: EventDto): EventDto => ({
      ...event,
      myPaymentStatus: 'MarkedPaid',
    });

    set({
      processingEventId: eventId,
      events: events.map(e => e.id === eventId ? updateEvent(e) : e),
      selectedEvent: selectedEvent?.id === eventId ? updateEvent(selectedEvent) : selectedEvent,
      myRegistrations: myRegistrations.map(e => e.id === eventId ? updateEvent(e) : e),
    });

    try {
      await eventService.markPayment(eventId);
      set({ processingEventId: null });
      return true;
    } catch (error) {
      // Rollback on failure
      set({
        events,
        selectedEvent,
        myRegistrations,
        processingEventId: null,
        error: error instanceof Error ? error.message : 'Failed to mark payment',
      });
      return false;
    }
  },

  // Update payment status (for organizers)
  updatePaymentStatus: async (eventId: string, registrationId: string, status: 'Verified' | 'Pending') => {
    try {
      await eventService.updatePaymentStatus(eventId, registrationId, { paymentStatus: status });
      // Refresh event data to get updated registrations
      await get().fetchEventById(eventId);
      return true;
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to update payment status',
      });
      return false;
    }
  },

  // Team assignment actions

  // Update team assignment for a registration (organizer only)
  updateTeamAssignment: async (eventId: string, registrationId: string, team: TeamAssignment) => {
    try {
      await eventService.updateTeamAssignment(eventId, registrationId, team);
      // Refresh event data to get updated team assignments
      await get().fetchEventById(eventId);
      return true;
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to update team assignment',
      });
      return false;
    }
  },

  // Registration management (organizer)

  // Remove a registration (organizer only)
  removeRegistration: async (eventId: string, registrationId: string) => {
    try {
      await eventService.removeRegistration(eventId, registrationId);
      // Refresh event data to get updated registrations
      await get().fetchEventById(eventId);
      return true;
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to remove registration',
      });
      return false;
    }
  },

  // Cancel/delete event (organizer only) - soft delete
  cancelEvent: async (eventId: string) => {
    const { events, myRegistrations } = get();

    try {
      console.log('🗑️ Deleting event:', eventId);
      await eventService.cancel(eventId);
      console.log('🗑️ Event deleted successfully');
      // Remove cancelled event from all lists
      set({
        events: events.filter(e => e.id !== eventId),
        myRegistrations: myRegistrations.filter(e => e.id !== eventId),
        selectedEvent: null,
      });
      return true;
    } catch (error: any) {
      console.error('🗑️ Delete event failed:', error?.response?.data || error?.message || error);
      set({
        error: error instanceof Error ? error.message : 'Failed to cancel event',
      });
      return false;
    }
  },
}));
