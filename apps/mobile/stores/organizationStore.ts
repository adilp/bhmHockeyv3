import { create } from 'zustand';
import { organizationService } from '@bhmhockey/api-client';
import type { Organization, OrganizationSubscription, CreateOrganizationRequest, OrganizationMember, AutoRosterMember, Position, OrganizationWaiver } from '@bhmhockey/shared';
import { useEventStore, getErrorMessage } from './eventStore';

interface OrganizationState {
  organizations: Organization[];
  mySubscriptions: OrganizationSubscription[];
  myOrganizations: Organization[]; // Organizations I created/own
  members: OrganizationMember[]; // Members of the org being viewed (admin flows)
  autoRoster: AutoRosterMember[]; // Auto-roster of the org being viewed (admin only)
  autoRosterOrgId: string | null; // Which org the loaded autoRoster belongs to
  waiver: OrganizationWaiver | null; // Active waiver of the org being viewed (null = none)
  waiverOrgId: string | null; // Which org the loaded waiver belongs to
  isLoading: boolean;
  error: string | null;

  // Actions
  fetchOrganizations: () => Promise<void>;
  fetchMySubscriptions: () => Promise<void>;
  fetchMyOrganizations: () => Promise<void>;
  createOrganization: (data: CreateOrganizationRequest) => Promise<Organization>;
  subscribe: (organizationId: string) => Promise<void>;
  unsubscribe: (organizationId: string) => Promise<void>;
  deleteOrganization: (organizationId: string) => Promise<boolean>;
  clearError: () => void;

  // Members (admin flows)
  fetchMembers: (organizationId: string) => Promise<void>;

  // Auto-roster actions (admin only)
  fetchAutoRoster: (organizationId: string) => Promise<void>;
  addAutoRosterMember: (organizationId: string, userId: string, position: Position) => Promise<boolean>;
  removeAutoRosterMember: (organizationId: string, userId: string) => Promise<boolean>;
  reorderAutoRoster: (organizationId: string, orderedUserIds: string[]) => Promise<boolean>;

  // Waiver actions (fetch for anyone; save is admin only)
  fetchWaiver: (organizationId: string) => Promise<OrganizationWaiver | null>;
  saveWaiver: (organizationId: string, text: string) => Promise<boolean>;
}

export const useOrganizationStore = create<OrganizationState>((set, get) => ({
  organizations: [],
  mySubscriptions: [],
  myOrganizations: [],
  members: [],
  autoRoster: [],
  autoRosterOrgId: null,
  waiver: null,
  waiverOrgId: null,
  isLoading: false,
  error: null,

  fetchOrganizations: async () => {
    try {
      set({ isLoading: true, error: null });
      const organizations = await organizationService.getAll();
      set({ organizations, isLoading: false });
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to fetch organizations',
        isLoading: false
      });
    }
  },

  fetchMySubscriptions: async () => {
    try {
      set({ isLoading: true, error: null });
      const mySubscriptions = await organizationService.getMySubscriptions();
      set({ mySubscriptions, isLoading: false });
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to fetch subscriptions',
        isLoading: false
      });
    }
  },

  fetchMyOrganizations: async () => {
    try {
      const myOrganizations = await organizationService.getMyOrganizations();
      set({ myOrganizations });
    } catch (error) {
      // Silently fail - user might not own any orgs
      console.log('Failed to fetch my organizations:', error);
    }
  },

  createOrganization: async (data: CreateOrganizationRequest) => {
    set({ isLoading: true, error: null });
    try {
      const newOrg = await organizationService.create(data);
      // Add to organizations list and myOrganizations list
      set((state) => ({
        organizations: [newOrg, ...state.organizations],
        myOrganizations: [newOrg, ...state.myOrganizations],
        isLoading: false
      }));
      return newOrg;
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to create organization',
        isLoading: false
      });
      throw error;
    }
  },

  subscribe: async (organizationId: string) => {
    const { organizations } = get();

    // Optimistic update
    set({
      organizations: organizations.map(org =>
        org.id === organizationId
          ? { ...org, isSubscribed: true, subscriberCount: org.subscriberCount + 1 }
          : org
      )
    });

    try {
      await organizationService.subscribe(organizationId);
      // Refresh subscriptions to get full data
      await get().fetchMySubscriptions();
      // Membership changes event visibility - refresh so the org's events
      // appear without a manual pull-to-refresh (failure is non-fatal)
      await useEventStore.getState().fetchEvents().catch(() => {});
    } catch (error) {
      // Rollback optimistic update - restore original state
      set({
        organizations,
        error: error instanceof Error ? error.message : 'Failed to subscribe'
      });
    }
  },

  unsubscribe: async (organizationId: string) => {
    const { organizations, mySubscriptions } = get();

    // Optimistic update
    set({
      organizations: organizations.map(org =>
        org.id === organizationId
          ? { ...org, isSubscribed: false, subscriberCount: org.subscriberCount - 1 }
          : org
      ),
      mySubscriptions: mySubscriptions.filter(sub => sub.organization.id !== organizationId)
    });

    try {
      await organizationService.unsubscribe(organizationId);
      // Membership changes event visibility - keep the events list current
      await useEventStore.getState().fetchEvents().catch(() => {});
    } catch (error) {
      // Rollback optimistic update
      set({
        organizations,
        mySubscriptions,
        error: error instanceof Error ? error.message : 'Failed to unsubscribe'
      });
    }
  },

  deleteOrganization: async (organizationId: string) => {
    const { organizations, myOrganizations, mySubscriptions } = get();

    try {
      await organizationService.delete(organizationId);
      // Remove deleted organization from all lists
      set({
        organizations: organizations.filter(org => org.id !== organizationId),
        myOrganizations: myOrganizations.filter(org => org.id !== organizationId),
        mySubscriptions: mySubscriptions.filter(sub => sub.organization.id !== organizationId),
      });
      return true;
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to delete organization',
      });
      return false;
    }
  },

  clearError: () => set({ error: null }),

  fetchMembers: async (organizationId: string) => {
    try {
      const members = await organizationService.getMembers(organizationId);
      set({ members });
    } catch (error) {
      set({ error: getErrorMessage(error, 'Failed to load members') });
    }
  },

  fetchAutoRoster: async (organizationId: string) => {
    try {
      const autoRoster = await organizationService.getAutoRoster(organizationId);
      set({ autoRoster, autoRosterOrgId: organizationId });
    } catch (error) {
      set({ error: getErrorMessage(error, 'Failed to load auto-roster') });
    }
  },

  addAutoRosterMember: async (organizationId: string, userId: string, position: Position) => {
    try {
      const member = await organizationService.addAutoRosterMember(organizationId, { userId, position });
      set((state) => ({ autoRoster: [...state.autoRoster, member], autoRosterOrgId: organizationId }));
      return true;
    } catch (error) {
      set({ error: getErrorMessage(error, 'Failed to add player to auto-roster') });
      return false;
    }
  },

  removeAutoRosterMember: async (organizationId: string, userId: string) => {
    const { autoRoster } = get();

    // Optimistic update
    set({ autoRoster: autoRoster.filter((m) => m.userId !== userId) });

    try {
      await organizationService.removeAutoRosterMember(organizationId, userId);
      return true;
    } catch (error) {
      // Rollback on failure
      set({
        autoRoster,
        error: getErrorMessage(error, 'Failed to remove player from auto-roster'),
      });
      return false;
    }
  },

  fetchWaiver: async (organizationId: string) => {
    try {
      const waiver = await organizationService.getWaiver(organizationId);
      set({ waiver, waiverOrgId: organizationId });
      return waiver;
    } catch (error) {
      // 404 = no active waiver; a normal state, not an error
      if (error && typeof error === 'object' && (error as any).statusCode === 404) {
        set({ waiver: null, waiverOrgId: organizationId });
        return null;
      }
      set({ error: getErrorMessage(error, 'Failed to load waiver') });
      return null;
    }
  },

  saveWaiver: async (organizationId: string, text: string) => {
    try {
      // Returns the new active waiver, or null when cleared/deactivated
      const waiver = await organizationService.setWaiver(organizationId, text);
      set({ waiver, waiverOrgId: organizationId });
      return true;
    } catch (error) {
      set({ error: getErrorMessage(error, 'Failed to save waiver') });
      return false;
    }
  },

  reorderAutoRoster: async (organizationId: string, orderedUserIds: string[]) => {
    const { autoRoster } = get();

    // Optimistic update - reorder locally
    const byUserId = new Map(autoRoster.map((m) => [m.userId, m]));
    const reordered = orderedUserIds
      .map((userId, index) => {
        const member = byUserId.get(userId);
        return member ? { ...member, sortOrder: index } : null;
      })
      .filter((m): m is AutoRosterMember => m !== null);
    set({ autoRoster: reordered });

    try {
      const updated = await organizationService.reorderAutoRoster(organizationId, orderedUserIds);
      set({ autoRoster: updated });
      return true;
    } catch (error) {
      // Rollback on failure
      set({
        autoRoster,
        error: getErrorMessage(error, 'Failed to reorder auto-roster'),
      });
      return false;
    }
  },
}));
