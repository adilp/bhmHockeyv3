import { create } from 'zustand';
import { organizationService } from '@bhmhockey/api-client';
import type { PendingWaiver, WaiverSignatureDetails } from '@bhmhockey/shared';
import { useOrganizationStore } from './organizationStore';
import { useEventStore } from './eventStore';

/** Extract message from ApiError objects or Error instances */
function getErrorMessage(error: unknown, fallback: string): string {
  if (error && typeof error === 'object' && 'message' in error && typeof (error as any).message === 'string') {
    return (error as any).message || fallback;
  }
  return fallback;
}

interface WaiverState {
  // Orgs where the user holds an upcoming registration but hasn't accepted the
  // current waiver. Non-empty => the blocking accept-or-leave gate is shown.
  pendingWaivers: PendingWaiver[];
  isFetching: boolean;
  error: string | null;

  // Actions
  fetchPendingWaivers: () => Promise<void>;
  acceptWaiver: (
    organizationId: string,
    waiverId: string,
    signature: WaiverSignatureDetails
  ) => Promise<boolean>;
  leaveOrganization: (organizationId: string) => Promise<boolean>;
  reset: () => void;
}

export const useWaiverStore = create<WaiverState>((set, get) => ({
  pendingWaivers: [],
  isFetching: false,
  error: null,

  // Fetch pending waivers (called on app open, foreground, and after login)
  fetchPendingWaivers: async () => {
    if (get().isFetching) {
      return;
    }
    set({ isFetching: true, error: null });
    try {
      const pendingWaivers = await organizationService.getPendingWaivers();
      set({ pendingWaivers, isFetching: false });
    } catch (error) {
      // Keep the current queue on error - retried on next foreground
      set({
        error: getErrorMessage(error, 'Failed to check pending waivers'),
        isFetching: false,
      });
    }
  },

  // Accept a specific waiver version, recording the signature fields captured
  // on the acceptance form; removes the org from the pending queue
  acceptWaiver: async (
    organizationId: string,
    waiverId: string,
    signature: WaiverSignatureDetails
  ) => {
    try {
      await organizationService.acceptWaiver(organizationId, waiverId, signature);
      set((state) => ({
        pendingWaivers: state.pendingWaivers.filter((p) => p.organizationId !== organizationId),
      }));
      return true;
    } catch (error) {
      const message = getErrorMessage(error, 'Failed to accept waiver');
      // Stale version (admin updated the waiver mid-flow): refresh the queue so
      // the gate re-presents the latest text (the refresh resets error state,
      // so set the acceptance error afterwards)
      await get().fetchPendingWaivers();
      set({ error: message });
      return false;
    }
  },

  // Leave the org (unsubscribes AND cancels upcoming registrations server-side);
  // removes the org from the pending queue
  leaveOrganization: async (organizationId: string) => {
    try {
      await organizationService.leaveOrganization(organizationId);
      set((state) => ({
        pendingWaivers: state.pendingWaivers.filter((p) => p.organizationId !== organizationId),
      }));
      // Leaving changes org membership AND cancels registrations - refresh the
      // org list and event lists so every screen is current without a manual
      // pull-to-refresh. Refresh failures don't undo the successful leave.
      await Promise.all([
        useOrganizationStore.getState().fetchOrganizations(),
        useEventStore.getState().fetchEvents(),
        useEventStore.getState().fetchMyRegistrations(),
      ]).catch(() => {});
      return true;
    } catch (error) {
      set({ error: getErrorMessage(error, 'Failed to leave organization') });
      return false;
    }
  },

  // Reset store (on logout)
  reset: () => set({ pendingWaivers: [], isFetching: false, error: null }),
}));
