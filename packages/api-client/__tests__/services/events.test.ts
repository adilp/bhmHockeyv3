/**
 * EventService Tests - Verifying the API client passes the waitlist-visibility
 * setting and payment calls through to the correct endpoints.
 */

// Mock AsyncStorage before any imports
jest.mock('@react-native-async-storage/async-storage', () => ({
  setItem: jest.fn(() => Promise.resolve()),
  getItem: jest.fn(() => Promise.resolve(null)),
  removeItem: jest.fn(() => Promise.resolve()),
}));

// Store axios mock functions
const mockPost = jest.fn();
const mockGet = jest.fn();
const mockPut = jest.fn();
const mockDelete = jest.fn();

// Mock axios
jest.mock('axios', () => ({
  create: jest.fn(() => ({
    interceptors: {
      request: { use: jest.fn() },
      response: { use: jest.fn() },
    },
    post: mockPost,
    get: mockGet,
    put: mockPut,
    delete: mockDelete,
  })),
}));

// Import after mocking
import { eventService } from '../../src/services/events';
import { initializeApiClient } from '../../src/client';

describe('eventService waitlist visibility & payment', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    initializeApiClient({ baseURL: 'http://localhost:5001/api' });
  });

  describe('create', () => {
    it('passes showWaitlistBeforePublish through to the create endpoint', async () => {
      const request = {
        eventDate: '2026-08-01T00:00:00Z',
        maxPlayers: 12,
        cost: 25,
        showWaitlistBeforePublish: true,
      };
      mockPost.mockResolvedValueOnce({ data: { id: 'event-1' } });

      await eventService.create(request);

      expect(mockPost).toHaveBeenCalledWith('/events', request);
    });
  });

  describe('update', () => {
    it('passes showWaitlistBeforePublish through to the update endpoint', async () => {
      mockPut.mockResolvedValueOnce({ data: { id: 'event-1' } });

      await eventService.update('event-1', { showWaitlistBeforePublish: false });

      expect(mockPut).toHaveBeenCalledWith('/events/event-1', {
        showWaitlistBeforePublish: false,
      });
    });
  });

  describe('markPayment', () => {
    it('posts to the mark-paid endpoint', async () => {
      mockPost.mockResolvedValueOnce({ data: {} });

      await eventService.markPayment('event-1');

      expect(mockPost).toHaveBeenCalledWith('/events/event-1/payment/mark-paid', {});
    });

    it('surfaces server rejection (ineligible waitlisted player)', async () => {
      const apiError = { message: "You're on the waitlist and there isn't an open spot for you yet - don't pay yet. The organizer will reach out if a spot opens." };
      mockPost.mockRejectedValueOnce(apiError);

      await expect(eventService.markPayment('event-1')).rejects.toEqual(apiError);
    });
  });

  describe('getRegistrations', () => {
    it('fetches registrations from the correct endpoint', async () => {
      mockGet.mockResolvedValueOnce({ data: [] });

      const result = await eventService.getRegistrations('event-1');

      expect(mockGet).toHaveBeenCalledWith('/events/event-1/registrations');
      expect(result).toEqual([]);
    });
  });

  describe('updateGhostPlayer', () => {
    it('puts the guest edit to the ghost-players endpoint', async () => {
      const request = {
        firstName: 'Jane',
        lastName: 'Smith',
        position: 'Goalie' as const,
        skillLevel: 'Silver' as const,
      };
      const updated = { id: 'reg-1', registeredPosition: 'Goalie' };
      mockPut.mockResolvedValueOnce({ data: updated });

      const result = await eventService.updateGhostPlayer('event-1', 'ghost-user-1', request);

      expect(mockPut).toHaveBeenCalledWith(
        '/events/event-1/registrations/ghost-players/ghost-user-1',
        request
      );
      expect(result).toEqual(updated);
    });

    it('surfaces server rejection (roster full for skaters)', async () => {
      const apiError = { message: 'The roster is full for skaters. Free up a skater spot before switching this guest from Goalie to Skater.' };
      mockPut.mockRejectedValueOnce(apiError);

      await expect(
        eventService.updateGhostPlayer('event-1', 'ghost-user-1', {
          firstName: 'Jane',
          lastName: 'Smith',
          position: 'Skater',
        })
      ).rejects.toEqual(apiError);
    });
  });
});
