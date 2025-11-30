/**
 * EventStore Tests - Protecting event registration state management
 * These tests ensure:
 * - Optimistic updates work correctly for registration/cancellation
 * - Rollback occurs on API failure
 * - processingEventId tracks in-flight operations
 * - Error states are properly managed
 */

// Mock functions must be defined before jest.mock
const mockGetAll = jest.fn();
const mockGetById = jest.fn();
const mockCreate = jest.fn();
const mockRegister = jest.fn();
const mockCancelRegistration = jest.fn();
const mockGetMyRegistrations = jest.fn();

// Mock the api-client module
jest.mock('@bhmhockey/api-client', () => ({
  eventService: {
    getAll: mockGetAll,
    getById: mockGetById,
    create: mockCreate,
    register: mockRegister,
    cancelRegistration: mockCancelRegistration,
    getMyRegistrations: mockGetMyRegistrations,
  },
}));

// Import after mocking
import { useEventStore } from '../../stores/eventStore';
import type { EventDto } from '@bhmhockey/shared';

const createMockEvent = (overrides: Partial<EventDto> = {}): EventDto => ({
  id: 'event-1',
  organizationId: 'org-1',
  organizationName: 'Test Org',
  creatorId: 'creator-1',
  name: 'Test Event',
  description: 'Test Description',
  eventDate: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
  duration: 60,
  venue: 'Test Venue',
  maxPlayers: 10,
  registeredCount: 5,
  cost: 25,
  status: 'Published',
  visibility: 'Public',
  isRegistered: false,
  canManage: false,
  createdAt: new Date().toISOString(),
  ...overrides,
});

describe('eventStore', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    // Reset store state
    useEventStore.setState({
      events: [],
      myRegistrations: [],
      selectedEvent: null,
      isLoading: false,
      isCreating: false,
      processingEventId: null,
      error: null,
    });
  });

  describe('fetchEvents', () => {
    it('sets events on successful fetch', async () => {
      const mockEvents = [
        createMockEvent({ id: 'event-1' }),
        createMockEvent({ id: 'event-2', name: 'Event 2' }),
      ];
      mockGetAll.mockResolvedValue(mockEvents);

      await useEventStore.getState().fetchEvents();

      expect(useEventStore.getState().events).toEqual(mockEvents);
      expect(useEventStore.getState().isLoading).toBe(false);
      expect(useEventStore.getState().error).toBeNull();
    });

    it('sets isLoading to true during fetch', async () => {
      let resolvePromise: (value: EventDto[]) => void;
      mockGetAll.mockReturnValue(
        new Promise((resolve) => {
          resolvePromise = resolve;
        })
      );

      const fetchPromise = useEventStore.getState().fetchEvents();

      expect(useEventStore.getState().isLoading).toBe(true);

      resolvePromise!([]);
      await fetchPromise;

      expect(useEventStore.getState().isLoading).toBe(false);
    });

    it('sets error on fetch failure', async () => {
      mockGetAll.mockRejectedValue(new Error('Network error'));

      await useEventStore.getState().fetchEvents();

      expect(useEventStore.getState().error).toBe('Network error');
      expect(useEventStore.getState().isLoading).toBe(false);
    });
  });

  describe('register', () => {
    it('optimistically updates UI before API response', async () => {
      const event = createMockEvent({ id: 'event-1', isRegistered: false, registeredCount: 5 });
      useEventStore.setState({ events: [event] });

      // Don't resolve the promise yet
      let resolveRegister: (value?: unknown) => void;
      mockRegister.mockReturnValue(
        new Promise((resolve) => {
          resolveRegister = resolve;
        })
      );

      // Start registration (don't await)
      const registerPromise = useEventStore.getState().register('event-1');

      // Check optimistic update happened immediately
      const updatedEvent = useEventStore.getState().events.find((e) => e.id === 'event-1');
      expect(updatedEvent?.isRegistered).toBe(true);
      expect(updatedEvent?.registeredCount).toBe(6);

      // processingEventId should be set
      expect(useEventStore.getState().processingEventId).toBe('event-1');

      // Now resolve
      resolveRegister!();
      await registerPromise;

      // processingEventId should be cleared
      expect(useEventStore.getState().processingEventId).toBeNull();
    });

    it('rolls back optimistic update on API error', async () => {
      const event = createMockEvent({ id: 'event-1', isRegistered: false, registeredCount: 5 });
      useEventStore.setState({ events: [event] });
      mockRegister.mockRejectedValue(new Error('Registration failed'));

      const result = await useEventStore.getState().register('event-1');

      // Should return false on failure
      expect(result).toBe(false);

      // Check rollback occurred
      const rolledBackEvent = useEventStore.getState().events.find((e) => e.id === 'event-1');
      expect(rolledBackEvent?.isRegistered).toBe(false);
      expect(rolledBackEvent?.registeredCount).toBe(5);
      expect(useEventStore.getState().processingEventId).toBeNull();
      expect(useEventStore.getState().error).toBe('Registration failed');
    });

    it('updates myRegistrations after successful registration', async () => {
      const event = createMockEvent({ id: 'event-1', isRegistered: false });
      useEventStore.setState({ events: [event], myRegistrations: [] });
      mockRegister.mockResolvedValue(undefined);

      await useEventStore.getState().register('event-1');

      expect(useEventStore.getState().myRegistrations).toHaveLength(1);
      expect(useEventStore.getState().myRegistrations[0].id).toBe('event-1');
      expect(useEventStore.getState().myRegistrations[0].isRegistered).toBe(true);
    });

    it('also updates selectedEvent if it matches', async () => {
      const event = createMockEvent({ id: 'event-1', isRegistered: false, registeredCount: 5 });
      useEventStore.setState({ events: [event], selectedEvent: event });
      mockRegister.mockResolvedValue(undefined);

      await useEventStore.getState().register('event-1');

      expect(useEventStore.getState().selectedEvent?.isRegistered).toBe(true);
      expect(useEventStore.getState().selectedEvent?.registeredCount).toBe(6);
    });
  });

  describe('cancelRegistration', () => {
    it('optimistically updates UI before API response', async () => {
      const event = createMockEvent({ id: 'event-1', isRegistered: true, registeredCount: 5 });
      useEventStore.setState({
        events: [event],
        myRegistrations: [event],
      });

      let resolveCancelReg: (value?: unknown) => void;
      mockCancelRegistration.mockReturnValue(
        new Promise((resolve) => {
          resolveCancelReg = resolve;
        })
      );

      // Start cancellation (don't await)
      const cancelPromise = useEventStore.getState().cancelRegistration('event-1');

      // Check optimistic update happened immediately
      const updatedEvent = useEventStore.getState().events.find((e) => e.id === 'event-1');
      expect(updatedEvent?.isRegistered).toBe(false);
      expect(updatedEvent?.registeredCount).toBe(4);
      expect(useEventStore.getState().myRegistrations).toHaveLength(0);
      expect(useEventStore.getState().processingEventId).toBe('event-1');

      resolveCancelReg!();
      await cancelPromise;

      expect(useEventStore.getState().processingEventId).toBeNull();
    });

    it('rolls back optimistic update on API error', async () => {
      const event = createMockEvent({ id: 'event-1', isRegistered: true, registeredCount: 5 });
      useEventStore.setState({
        events: [event],
        myRegistrations: [event],
      });
      mockCancelRegistration.mockRejectedValue(new Error('Cancel failed'));

      const result = await useEventStore.getState().cancelRegistration('event-1');

      expect(result).toBe(false);

      // Check rollback occurred
      const rolledBackEvent = useEventStore.getState().events.find((e) => e.id === 'event-1');
      expect(rolledBackEvent?.isRegistered).toBe(true);
      expect(rolledBackEvent?.registeredCount).toBe(5);
      expect(useEventStore.getState().myRegistrations).toHaveLength(1);
      expect(useEventStore.getState().processingEventId).toBeNull();
      expect(useEventStore.getState().error).toBe('Cancel failed');
    });

    it('also updates selectedEvent if it matches', async () => {
      const event = createMockEvent({ id: 'event-1', isRegistered: true, registeredCount: 5 });
      useEventStore.setState({ events: [event], selectedEvent: event, myRegistrations: [event] });
      mockCancelRegistration.mockResolvedValue(undefined);

      await useEventStore.getState().cancelRegistration('event-1');

      expect(useEventStore.getState().selectedEvent?.isRegistered).toBe(false);
      expect(useEventStore.getState().selectedEvent?.registeredCount).toBe(4);
    });
  });

  describe('createEvent', () => {
    it('adds new event to list on success', async () => {
      const existingEvent = createMockEvent({ id: 'existing-event' });
      useEventStore.setState({ events: [existingEvent] });

      const newEvent = createMockEvent({ id: 'new-event', name: 'New Event' });
      mockCreate.mockResolvedValue(newEvent);

      const result = await useEventStore.getState().createEvent({
        name: 'New Event',
        eventDate: new Date().toISOString(),
        duration: 60,
        maxPlayers: 10,
        cost: 0,
      });

      expect(result).toEqual(newEvent);
      expect(useEventStore.getState().events).toHaveLength(2);
      expect(useEventStore.getState().isCreating).toBe(false);
    });

    it('sets isCreating to true during creation', async () => {
      let resolveCreate: (value: EventDto) => void;
      mockCreate.mockReturnValue(
        new Promise((resolve) => {
          resolveCreate = resolve;
        })
      );

      const createPromise = useEventStore.getState().createEvent({
        name: 'New Event',
        eventDate: new Date().toISOString(),
        duration: 60,
        maxPlayers: 10,
        cost: 0,
      });

      expect(useEventStore.getState().isCreating).toBe(true);

      resolveCreate!(createMockEvent());
      await createPromise;

      expect(useEventStore.getState().isCreating).toBe(false);
    });

    it('returns null and sets error on failure', async () => {
      mockCreate.mockRejectedValue(new Error('Create failed'));

      const result = await useEventStore.getState().createEvent({
        name: 'New Event',
        eventDate: new Date().toISOString(),
        duration: 60,
        maxPlayers: 10,
        cost: 0,
      });

      expect(result).toBeNull();
      expect(useEventStore.getState().error).toBe('Create failed');
      expect(useEventStore.getState().isCreating).toBe(false);
    });
  });

  describe('fetchEventById', () => {
    it('sets selectedEvent on successful fetch', async () => {
      const event = createMockEvent({ id: 'event-1' });
      mockGetById.mockResolvedValue(event);

      await useEventStore.getState().fetchEventById('event-1');

      expect(useEventStore.getState().selectedEvent).toEqual(event);
      expect(useEventStore.getState().isLoading).toBe(false);
    });

    it('sets error on fetch failure', async () => {
      mockGetById.mockRejectedValue(new Error('Event not found'));

      await useEventStore.getState().fetchEventById('event-1');

      expect(useEventStore.getState().error).toBe('Event not found');
      expect(useEventStore.getState().selectedEvent).toBeNull();
    });
  });

  describe('clearSelectedEvent', () => {
    it('clears selected event', () => {
      const event = createMockEvent();
      useEventStore.setState({ selectedEvent: event });

      useEventStore.getState().clearSelectedEvent();

      expect(useEventStore.getState().selectedEvent).toBeNull();
    });
  });

  describe('clearError', () => {
    it('clears error state', () => {
      useEventStore.setState({ error: 'Some error' });

      useEventStore.getState().clearError();

      expect(useEventStore.getState().error).toBeNull();
    });
  });

  describe('processingEventId prevents double-clicks', () => {
    it('tracks which event is being processed', async () => {
      const event = createMockEvent({ id: 'event-1' });
      useEventStore.setState({ events: [event] });

      let resolveRegister: (value?: unknown) => void;
      mockRegister.mockReturnValue(
        new Promise((resolve) => {
          resolveRegister = resolve;
        })
      );

      // Start registration
      const promise = useEventStore.getState().register('event-1');

      // processingEventId should be set
      expect(useEventStore.getState().processingEventId).toBe('event-1');

      resolveRegister!();
      await promise;

      // Should be cleared after completion
      expect(useEventStore.getState().processingEventId).toBeNull();
    });
  });
});
