/**
 * WaiverStore Tests - Protecting the blocking accept-or-leave gate queue.
 * These tests ensure the pending queue advances on accept/leave and that
 * failures surface errors without corrupting the queue.
 */

// Mock functions must be defined before jest.mock
const mockGetPendingWaivers = jest.fn();
const mockAcceptWaiver = jest.fn();
const mockLeaveOrganization = jest.fn();

// Mock the api-client module
jest.mock('@bhmhockey/api-client', () => ({
  organizationService: {
    getPendingWaivers: mockGetPendingWaivers,
    acceptWaiver: mockAcceptWaiver,
    leaveOrganization: mockLeaveOrganization,
  },
}));

// Import after mocking
import { useWaiverStore } from '../../stores/waiverStore';
import type { PendingWaiver } from '@bhmhockey/shared';

const createMockPendingWaiver = (overrides: Partial<PendingWaiver> = {}): PendingWaiver => ({
  organizationId: 'org-1',
  organizationName: 'Test Org',
  waiver: {
    id: 'waiver-1',
    organizationId: 'org-1',
    text: 'You play at your own risk.',
    version: 1,
    createdAt: new Date().toISOString(),
  },
  ...overrides,
});

describe('waiverStore', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useWaiverStore.setState({
      pendingWaivers: [],
      isFetching: false,
      error: null,
    });
  });

  describe('fetchPendingWaivers', () => {
    it('sets the pending queue on successful fetch', async () => {
      const pending = [createMockPendingWaiver()];
      mockGetPendingWaivers.mockResolvedValue(pending);

      await useWaiverStore.getState().fetchPendingWaivers();

      expect(useWaiverStore.getState().pendingWaivers).toEqual(pending);
      expect(useWaiverStore.getState().isFetching).toBe(false);
      expect(useWaiverStore.getState().error).toBeNull();
    });

    it('keeps the current queue and sets error on failure', async () => {
      const existing = [createMockPendingWaiver()];
      useWaiverStore.setState({ pendingWaivers: existing });
      mockGetPendingWaivers.mockRejectedValue(new Error('Network error'));

      await useWaiverStore.getState().fetchPendingWaivers();

      expect(useWaiverStore.getState().pendingWaivers).toEqual(existing);
      expect(useWaiverStore.getState().error).toBe('Network error');
      expect(useWaiverStore.getState().isFetching).toBe(false);
    });

    it('skips duplicate concurrent fetches', async () => {
      let resolveFetch: (value: PendingWaiver[]) => void;
      mockGetPendingWaivers.mockReturnValue(
        new Promise((resolve) => {
          resolveFetch = resolve;
        })
      );

      const first = useWaiverStore.getState().fetchPendingWaivers();
      const second = useWaiverStore.getState().fetchPendingWaivers();

      resolveFetch!([]);
      await Promise.all([first, second]);

      expect(mockGetPendingWaivers).toHaveBeenCalledTimes(1);
    });
  });

  describe('acceptWaiver', () => {
    it('removes the org from the queue on success', async () => {
      const org1 = createMockPendingWaiver();
      const org2 = createMockPendingWaiver({
        organizationId: 'org-2',
        organizationName: 'Second Org',
        waiver: { id: 'waiver-2', organizationId: 'org-2', text: 'text', version: 1, createdAt: new Date().toISOString() },
      });
      useWaiverStore.setState({ pendingWaivers: [org1, org2] });
      mockAcceptWaiver.mockResolvedValue(undefined);

      const result = await useWaiverStore.getState().acceptWaiver('org-1', 'waiver-1');

      expect(result).toBe(true);
      expect(mockAcceptWaiver).toHaveBeenCalledWith('org-1', 'waiver-1');
      // Queue advances to the next org
      expect(useWaiverStore.getState().pendingWaivers).toEqual([org2]);
    });

    it('sets error, refreshes the queue, and returns false on failure', async () => {
      const stale = createMockPendingWaiver();
      useWaiverStore.setState({ pendingWaivers: [stale] });
      mockAcceptWaiver.mockRejectedValue(new Error('This waiver version is no longer current.'));
      const refreshed = [createMockPendingWaiver({
        waiver: { ...stale.waiver, id: 'waiver-v2', version: 2 },
      })];
      mockGetPendingWaivers.mockResolvedValue(refreshed);

      const result = await useWaiverStore.getState().acceptWaiver('org-1', 'waiver-1');

      expect(result).toBe(false);
      expect(useWaiverStore.getState().error).toBe('This waiver version is no longer current.');
      // Stale-version protection: queue re-fetched so the latest text shows
      expect(mockGetPendingWaivers).toHaveBeenCalled();
      expect(useWaiverStore.getState().pendingWaivers).toEqual(refreshed);
    });
  });

  describe('leaveOrganization', () => {
    it('removes the org from the queue on success', async () => {
      const pending = createMockPendingWaiver();
      useWaiverStore.setState({ pendingWaivers: [pending] });
      mockLeaveOrganization.mockResolvedValue(undefined);

      const result = await useWaiverStore.getState().leaveOrganization('org-1');

      expect(result).toBe(true);
      expect(mockLeaveOrganization).toHaveBeenCalledWith('org-1');
      expect(useWaiverStore.getState().pendingWaivers).toEqual([]);
    });

    it('keeps the queue and sets error on failure', async () => {
      const pending = createMockPendingWaiver();
      useWaiverStore.setState({ pendingWaivers: [pending] });
      mockLeaveOrganization.mockRejectedValue(new Error('Leave failed'));

      const result = await useWaiverStore.getState().leaveOrganization('org-1');

      expect(result).toBe(false);
      expect(useWaiverStore.getState().pendingWaivers).toEqual([pending]);
      expect(useWaiverStore.getState().error).toBe('Leave failed');
    });
  });

  describe('reset', () => {
    it('clears the queue and error state', () => {
      useWaiverStore.setState({
        pendingWaivers: [createMockPendingWaiver()],
        error: 'Some error',
      });

      useWaiverStore.getState().reset();

      expect(useWaiverStore.getState().pendingWaivers).toEqual([]);
      expect(useWaiverStore.getState().error).toBeNull();
    });
  });
});
