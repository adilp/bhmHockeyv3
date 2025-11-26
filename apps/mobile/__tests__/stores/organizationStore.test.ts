/**
 * OrganizationStore Tests - Protecting subscription state management
 * These tests ensure optimistic updates work correctly and rollback on failure.
 */

// Mock functions must be defined before jest.mock
const mockGetAll = jest.fn();
const mockGetMySubscriptions = jest.fn();
const mockSubscribe = jest.fn();
const mockUnsubscribe = jest.fn();

// Mock the api-client module
jest.mock('@bhmhockey/api-client', () => ({
  organizationService: {
    getAll: mockGetAll,
    getMySubscriptions: mockGetMySubscriptions,
    subscribe: mockSubscribe,
    unsubscribe: mockUnsubscribe,
  },
}));

// Import after mocking
import { useOrganizationStore } from '../../stores/organizationStore';
import type { Organization, OrganizationSubscription } from '@bhmhockey/shared';

const createMockOrg = (overrides: Partial<Organization> = {}): Organization => ({
  id: 'org-1',
  name: 'Test Org',
  description: 'Test Description',
  location: 'Boston',
  skillLevel: 'Gold' as const,
  creatorId: 'creator-1',
  subscriberCount: 5,
  isSubscribed: false,
  createdAt: new Date().toISOString(),
  ...overrides,
});

const createMockSubscription = (org: Organization): OrganizationSubscription => ({
  id: 'sub-1',
  organization: org,
  notificationEnabled: true,
  subscribedAt: new Date().toISOString(),
});

describe('organizationStore', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    // Reset store state
    useOrganizationStore.setState({
      organizations: [],
      mySubscriptions: [],
      isLoading: false,
      error: null,
    });
  });

  describe('fetchOrganizations', () => {
    it('sets organizations on successful fetch', async () => {
      const mockOrgs = [createMockOrg({ id: 'org-1' }), createMockOrg({ id: 'org-2', name: 'Org 2' })];
      mockGetAll.mockResolvedValue(mockOrgs);

      await useOrganizationStore.getState().fetchOrganizations();

      expect(useOrganizationStore.getState().organizations).toEqual(mockOrgs);
      expect(useOrganizationStore.getState().isLoading).toBe(false);
      expect(useOrganizationStore.getState().error).toBeNull();
    });

    it('sets isLoading to true during fetch', async () => {
      let resolvePromise: (value: Organization[]) => void;
      mockGetAll.mockReturnValue(
        new Promise((resolve) => {
          resolvePromise = resolve;
        })
      );

      const fetchPromise = useOrganizationStore.getState().fetchOrganizations();

      expect(useOrganizationStore.getState().isLoading).toBe(true);

      resolvePromise!([]);
      await fetchPromise;

      expect(useOrganizationStore.getState().isLoading).toBe(false);
    });

    it('sets error on fetch failure', async () => {
      mockGetAll.mockRejectedValue(new Error('Network error'));

      await useOrganizationStore.getState().fetchOrganizations();

      expect(useOrganizationStore.getState().error).toBe('Network error');
      expect(useOrganizationStore.getState().isLoading).toBe(false);
    });

    it('clears previous error on successful fetch', async () => {
      useOrganizationStore.setState({ error: 'Previous error' });
      mockGetAll.mockResolvedValue([]);

      await useOrganizationStore.getState().fetchOrganizations();

      expect(useOrganizationStore.getState().error).toBeNull();
    });
  });

  describe('subscribe', () => {
    it('optimistically updates UI before API response', async () => {
      const org = createMockOrg({ id: 'org-1', isSubscribed: false, subscriberCount: 5 });
      useOrganizationStore.setState({ organizations: [org] });

      // Don't resolve the promise yet
      let resolveSubscribe: (value?: unknown) => void;
      mockSubscribe.mockReturnValue(
        new Promise((resolve) => {
          resolveSubscribe = resolve;
        })
      );
      mockGetMySubscriptions.mockResolvedValue([]);

      // Start subscription (don't await)
      const subscribePromise = useOrganizationStore.getState().subscribe('org-1');

      // Check optimistic update happened immediately
      const updatedOrg = useOrganizationStore.getState().organizations.find((o) => o.id === 'org-1');
      expect(updatedOrg?.isSubscribed).toBe(true);
      expect(updatedOrg?.subscriberCount).toBe(6);

      // Now resolve
      resolveSubscribe!();
      await subscribePromise;
    });

    it('rolls back optimistic update on API error', async () => {
      const org = createMockOrg({ id: 'org-1', isSubscribed: false, subscriberCount: 5 });
      useOrganizationStore.setState({ organizations: [org] });
      mockSubscribe.mockRejectedValue(new Error('Subscribe failed'));

      await useOrganizationStore.getState().subscribe('org-1');

      // Check rollback occurred
      const rolledBackOrg = useOrganizationStore.getState().organizations.find((o) => o.id === 'org-1');
      expect(rolledBackOrg?.isSubscribed).toBe(false);
      expect(rolledBackOrg?.subscriberCount).toBe(5);
      expect(useOrganizationStore.getState().error).toBe('Subscribe failed');
    });

    it('refreshes subscriptions after successful subscribe', async () => {
      const org = createMockOrg({ id: 'org-1' });
      useOrganizationStore.setState({ organizations: [org] });
      mockSubscribe.mockResolvedValue(undefined);
      mockGetMySubscriptions.mockResolvedValue([createMockSubscription(org)]);

      await useOrganizationStore.getState().subscribe('org-1');

      expect(mockGetMySubscriptions).toHaveBeenCalled();
    });
  });

  describe('unsubscribe', () => {
    it('optimistically updates UI before API response', async () => {
      const org = createMockOrg({ id: 'org-1', isSubscribed: true, subscriberCount: 5 });
      const subscription = createMockSubscription(org);
      useOrganizationStore.setState({
        organizations: [org],
        mySubscriptions: [subscription],
      });

      let resolveUnsubscribe: (value?: unknown) => void;
      mockUnsubscribe.mockReturnValue(
        new Promise((resolve) => {
          resolveUnsubscribe = resolve;
        })
      );

      // Start unsubscription (don't await)
      const unsubscribePromise = useOrganizationStore.getState().unsubscribe('org-1');

      // Check optimistic update happened immediately
      const updatedOrg = useOrganizationStore.getState().organizations.find((o) => o.id === 'org-1');
      expect(updatedOrg?.isSubscribed).toBe(false);
      expect(updatedOrg?.subscriberCount).toBe(4);
      expect(useOrganizationStore.getState().mySubscriptions).toHaveLength(0);

      resolveUnsubscribe!();
      await unsubscribePromise;
    });

    it('rolls back optimistic update on API error', async () => {
      const org = createMockOrg({ id: 'org-1', isSubscribed: true, subscriberCount: 5 });
      const subscription = createMockSubscription(org);
      useOrganizationStore.setState({
        organizations: [org],
        mySubscriptions: [subscription],
      });
      mockUnsubscribe.mockRejectedValue(new Error('Unsubscribe failed'));

      await useOrganizationStore.getState().unsubscribe('org-1');

      // Check rollback occurred
      const rolledBackOrg = useOrganizationStore.getState().organizations.find((o) => o.id === 'org-1');
      expect(rolledBackOrg?.isSubscribed).toBe(true);
      expect(rolledBackOrg?.subscriberCount).toBe(5);
      expect(useOrganizationStore.getState().mySubscriptions).toHaveLength(1);
      expect(useOrganizationStore.getState().error).toBe('Unsubscribe failed');
    });
  });

  describe('fetchMySubscriptions', () => {
    it('sets subscriptions on successful fetch', async () => {
      const org = createMockOrg();
      const subscription = createMockSubscription(org);
      mockGetMySubscriptions.mockResolvedValue([subscription]);

      await useOrganizationStore.getState().fetchMySubscriptions();

      expect(useOrganizationStore.getState().mySubscriptions).toEqual([subscription]);
      expect(useOrganizationStore.getState().isLoading).toBe(false);
    });

    it('sets error on fetch failure', async () => {
      mockGetMySubscriptions.mockRejectedValue(new Error('Failed to fetch'));

      await useOrganizationStore.getState().fetchMySubscriptions();

      expect(useOrganizationStore.getState().error).toBe('Failed to fetch');
    });
  });

  describe('clearError', () => {
    it('clears error state', () => {
      useOrganizationStore.setState({ error: 'Some error' });

      useOrganizationStore.getState().clearError();

      expect(useOrganizationStore.getState().error).toBeNull();
    });
  });

  describe('createOrganization', () => {
    const mockCreate = jest.fn();

    beforeEach(() => {
      // Add create to the mock
      jest.requireMock('@bhmhockey/api-client').organizationService.create = mockCreate;
    });

    it('adds new organization to list on success', async () => {
      const existingOrg = createMockOrg({ id: 'existing-org' });
      useOrganizationStore.setState({ organizations: [existingOrg] });

      const newOrg = createMockOrg({ id: 'new-org', name: 'New Org' });
      mockCreate.mockResolvedValue(newOrg);

      const result = await useOrganizationStore.getState().createOrganization({
        name: 'New Org',
      });

      expect(result).toEqual(newOrg);
      expect(useOrganizationStore.getState().organizations).toHaveLength(2);
      // New org should be at the beginning
      expect(useOrganizationStore.getState().organizations[0].id).toBe('new-org');
    });

    it('sets loading state during creation', async () => {
      let resolveCreate: (value: Organization) => void;
      mockCreate.mockReturnValue(
        new Promise((resolve) => {
          resolveCreate = resolve;
        })
      );

      const createPromise = useOrganizationStore.getState().createOrganization({
        name: 'New Org',
      });

      expect(useOrganizationStore.getState().isLoading).toBe(true);

      resolveCreate!(createMockOrg());
      await createPromise;

      expect(useOrganizationStore.getState().isLoading).toBe(false);
    });

    it('sets error and throws on API failure', async () => {
      mockCreate.mockRejectedValue(new Error('Create failed'));

      await expect(
        useOrganizationStore.getState().createOrganization({ name: 'New Org' })
      ).rejects.toThrow('Create failed');

      expect(useOrganizationStore.getState().error).toBe('Create failed');
      expect(useOrganizationStore.getState().isLoading).toBe(false);
    });

    it('does not add org to list on failure', async () => {
      const existingOrg = createMockOrg({ id: 'existing-org' });
      useOrganizationStore.setState({ organizations: [existingOrg] });
      mockCreate.mockRejectedValue(new Error('Create failed'));

      try {
        await useOrganizationStore.getState().createOrganization({ name: 'New Org' });
      } catch {
        // Expected to throw
      }

      expect(useOrganizationStore.getState().organizations).toHaveLength(1);
      expect(useOrganizationStore.getState().organizations[0].id).toBe('existing-org');
    });
  });
});
