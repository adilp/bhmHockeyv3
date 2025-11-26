import { create } from 'zustand';
import { eventService } from '@bhmhockey/api-client';
import type { EventDto, CreateEventRequest } from '@bhmhockey/shared';

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
  register: (eventId: string) => Promise<boolean>;
  cancelRegistration: (eventId: string) => Promise<boolean>;
  clearSelectedEvent: () => void;
  clearError: () => void;
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
      set({ events, isLoading: false });
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
      set({ myRegistrations });
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
  register: async (eventId: string) => {
    const { events, selectedEvent, myRegistrations } = get();

    // Helper to update event registration state
    const updateEvent = (event: EventDto): EventDto => ({
      ...event,
      isRegistered: true,
      registeredCount: event.registeredCount + 1,
    });

    // Optimistic update + track processing
    set({
      processingEventId: eventId,
      events: events.map(e => e.id === eventId ? updateEvent(e) : e),
      selectedEvent: selectedEvent?.id === eventId ? updateEvent(selectedEvent) : selectedEvent,
    });

    try {
      await eventService.register(eventId);

      // Add to my registrations
      const registeredEvent = events.find(e => e.id === eventId);
      if (registeredEvent) {
        set({ myRegistrations: [...myRegistrations, updateEvent(registeredEvent)], processingEventId: null });
      } else {
        set({ processingEventId: null });
      }

      return true;
    } catch (error) {
      // Rollback on failure
      set({
        events,
        selectedEvent,
        processingEventId: null,
        error: error instanceof Error ? error.message : 'Failed to register'
      });
      return false;
    }
  },

  // Cancel registration (with optimistic update)
  cancelRegistration: async (eventId: string) => {
    const { events, selectedEvent, myRegistrations } = get();

    // Helper to update event registration state
    const updateEvent = (event: EventDto): EventDto => ({
      ...event,
      isRegistered: false,
      registeredCount: Math.max(0, event.registeredCount - 1),
    });

    // Optimistic update + track processing
    set({
      processingEventId: eventId,
      events: events.map(e => e.id === eventId ? updateEvent(e) : e),
      selectedEvent: selectedEvent?.id === eventId ? updateEvent(selectedEvent) : selectedEvent,
      myRegistrations: myRegistrations.filter(e => e.id !== eventId),
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
}));
