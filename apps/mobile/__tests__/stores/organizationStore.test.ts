/**
 * OrganizationStore Tests - Protecting subscription state management
 * These tests ensure optimistic updates work correctly and rollback on failure.
 */

// Mock functions must be defined before jest.mock
const mockGetAll = jest.fn();
const mockGetMySubscriptions = jest.fn();
const mockSubscribe = jest.fn();
const mockUnsubscribe = jest.fn();
const mockGetAutoRoster = jest.fn();
const mockAddAutoRosterMember = jest.fn();
const mockRemoveAutoRosterMember = jest.fn();
const mockReorderAutoRoster = jest.fn();

// Mock the api-client module
jest.mock('@bhmhockey/api-client', () => ({
  organizationService: {
    getAll: mockGetAll,
    getMySubscriptions: mockGetMySubscriptions,
    subscribe: mockSubscribe,
    unsubscribe: mockUnsubscribe,
    getAutoRoster: mockGetAutoRoster,
    addAutoRosterMember: mockAddAutoRosterMember,
    removeAutoRosterMember: mockRemoveAutoRosterMember,
    reorderAutoRoster: mockReorderAutoRoster,
  },
}));

// Import after mocking
import { useOrganizationStore } from '../../stores/organizationStore';
import type { Organization, OrganizationSubscription, AutoRosterMember } from '@bhmhockey/shared';

const createMockOrg = (overrides: Partial<Organization> = {}): Organization => ({
  id: 'org-1',
  name: 'Test Org',
  description: 'Test Description',
  location: 'Boston',
  skillLevels: ['Gold'] as const,
  creatorId: 'creator-1',
  subscriberCount: 5,
  isSubscribed: false,
  isAdmin: false,
  createdAt: new Date().toISOString(),
  ...overrides,
});

const createMockSubscription = (org: Organization): OrganizationSubscription => ({
  id: 'sub-1',
  organization: org,
  notificationEnabled: true,
  subscribedAt: new Date().toISOString(),
});

const createMockAutoRosterMember = (overrides: Partial<AutoRosterMember> = {}): AutoRosterMember => ({
  id: 'member-1',
  userId: 'user-1',
  firstName: 'Test',
  lastName: 'Player',
  positions: { skater: 'Silver' },
  position: 'Skater',
  sortOrder: 0,
  addedAt: new Date().toISOString(),
  ...overrides,
});

describe('organizationStore', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    // Reset store state
    useOrganizationStore.setState({
      organizations: [],
      mySubscriptions: [],
      autoRoster: [],
      autoRosterOrgId: null,
      members: [],
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

  describe('fetchAutoRoster', () => {
    it('sets autoRoster and org id on successful fetch', async () => {
      const members = [createMockAutoRosterMember()];
      mockGetAutoRoster.mockResolvedValue(members);

      await useOrganizationStore.getState().fetchAutoRoster('org-1');

      expect(mockGetAutoRoster).toHaveBeenCalledWith('org-1');
      expect(useOrganizationStore.getState().autoRoster).toEqual(members);
      expect(useOrganizationStore.getState().autoRosterOrgId).toBe('org-1');
    });

    it('sets error on fetch failure', async () => {
      mockGetAutoRoster.mockRejectedValue(new Error('Forbidden'));

      await useOrganizationStore.getState().fetchAutoRoster('org-1');

      expect(useOrganizationStore.getState().error).toBe('Forbidden');
    });
  });

  describe('addAutoRosterMember', () => {
    it('appends the new member on success', async () => {
      const existing = createMockAutoRosterMember({ userId: 'user-1', sortOrder: 0 });
      useOrganizationStore.setState({ autoRoster: [existing], autoRosterOrgId: 'org-1' });
      const added = createMockAutoRosterMember({ id: 'member-2', userId: 'user-2', sortOrder: 1, position: 'Goalie' });
      mockAddAutoRosterMember.mockResolvedValue(added);

      const result = await useOrganizationStore.getState().addAutoRosterMember('org-1', 'user-2', 'Goalie');

      expect(result).toBe(true);
      expect(mockAddAutoRosterMember).toHaveBeenCalledWith('org-1', { userId: 'user-2', position: 'Goalie' });
      expect(useOrganizationStore.getState().autoRoster).toEqual([existing, added]);
    });

    it('sets error and returns false on failure', async () => {
      mockAddAutoRosterMember.mockRejectedValue(new Error('User is already in the auto-roster'));

      const result = await useOrganizationStore.getState().addAutoRosterMember('org-1', 'user-2', 'Skater');

      expect(result).toBe(false);
      expect(useOrganizationStore.getState().error).toBe('User is already in the auto-roster');
      expect(useOrganizationStore.getState().autoRoster).toHaveLength(0);
    });
  });

  describe('removeAutoRosterMember', () => {
    it('optimistically removes the member', async () => {
      const member1 = createMockAutoRosterMember({ userId: 'user-1' });
      const member2 = createMockAutoRosterMember({ id: 'member-2', userId: 'user-2', sortOrder: 1 });
      useOrganizationStore.setState({ autoRoster: [member1, member2] });
      mockRemoveAutoRosterMember.mockResolvedValue(undefined);

      const result = await useOrganizationStore.getState().removeAutoRosterMember('org-1', 'user-1');

      expect(result).toBe(true);
      expect(useOrganizationStore.getState().autoRoster).toEqual([member2]);
    });

    it('rolls back on failure', async () => {
      const member = createMockAutoRosterMember({ userId: 'user-1' });
      useOrganizationStore.setState({ autoRoster: [member] });
      mockRemoveAutoRosterMember.mockRejectedValue(new Error('Remove failed'));

      const result = await useOrganizationStore.getState().removeAutoRosterMember('org-1', 'user-1');

      expect(result).toBe(false);
      expect(useOrganizationStore.getState().autoRoster).toEqual([member]);
      expect(useOrganizationStore.getState().error).toBe('Remove failed');
    });
  });

  describe('reorderAutoRoster', () => {
    it('applies the server order on success', async () => {
      const member1 = createMockAutoRosterMember({ userId: 'user-1', sortOrder: 0 });
      const member2 = createMockAutoRosterMember({ id: 'member-2', userId: 'user-2', sortOrder: 1 });
      useOrganizationStore.setState({ autoRoster: [member1, member2] });
      const reordered = [
        { ...member2, sortOrder: 0 },
        { ...member1, sortOrder: 1 },
      ];
      mockReorderAutoRoster.mockResolvedValue(reordered);

      const result = await useOrganizationStore.getState().reorderAutoRoster('org-1', ['user-2', 'user-1']);

      expect(result).toBe(true);
      expect(mockReorderAutoRoster).toHaveBeenCalledWith('org-1', ['user-2', 'user-1']);
      expect(useOrganizationStore.getState().autoRoster.map((m) => m.userId)).toEqual(['user-2', 'user-1']);
    });

    it('rolls back to the original order on failure', async () => {
      const member1 = createMockAutoRosterMember({ userId: 'user-1', sortOrder: 0 });
      const member2 = createMockAutoRosterMember({ id: 'member-2', userId: 'user-2', sortOrder: 1 });
      useOrganizationStore.setState({ autoRoster: [member1, member2] });
      mockReorderAutoRoster.mockRejectedValue(new Error('Reorder failed'));

      const result = await useOrganizationStore.getState().reorderAutoRoster('org-1', ['user-2', 'user-1']);

      expect(result).toBe(false);
      expect(useOrganizationStore.getState().autoRoster.map((m) => m.userId)).toEqual(['user-1', 'user-2']);
      expect(useOrganizationStore.getState().error).toBe('Reorder failed');
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
