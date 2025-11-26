import { create } from 'zustand';
import { organizationService } from '@bhmhockey/api-client';
import type { Organization, OrganizationSubscription, CreateOrganizationRequest } from '@bhmhockey/shared';

interface OrganizationState {
  organizations: Organization[];
  mySubscriptions: OrganizationSubscription[];
  isLoading: boolean;
  error: string | null;

  // Actions
  fetchOrganizations: () => Promise<void>;
  fetchMySubscriptions: () => Promise<void>;
  createOrganization: (data: CreateOrganizationRequest) => Promise<Organization>;
  subscribe: (organizationId: string) => Promise<void>;
  unsubscribe: (organizationId: string) => Promise<void>;
  clearError: () => void;
}

export const useOrganizationStore = create<OrganizationState>((set, get) => ({
  organizations: [],
  mySubscriptions: [],
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

  createOrganization: async (data: CreateOrganizationRequest) => {
    set({ isLoading: true, error: null });
    try {
      const newOrg = await organizationService.create(data);
      // Add to organizations list
      set((state) => ({
        organizations: [newOrg, ...state.organizations],
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
    } catch (error) {
      // Rollback optimistic update
      set({
        organizations,
        mySubscriptions,
        error: error instanceof Error ? error.message : 'Failed to unsubscribe'
      });
    }
  },

  clearError: () => set({ error: null }),
}));
