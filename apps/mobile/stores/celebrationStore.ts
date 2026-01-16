import { create } from 'zustand';
import { userService } from '@bhmhockey/api-client';
import type { UncelebratedBadgeDto } from '@bhmhockey/shared';
import { router } from 'expo-router';

interface CelebrationState {
  // State
  celebrationQueue: UncelebratedBadgeDto[];
  isShowingCelebration: boolean;
  isFetching: boolean;
  error: string | null;

  // Actions
  fetchUncelebrated: () => Promise<void>;
  celebrateCurrent: () => Promise<void>;
  navigateToTrophyCase: () => void;
  reset: () => void;
}

export const useCelebrationStore = create<CelebrationState>((set, get) => ({
  // Initial state
  celebrationQueue: [],
  isShowingCelebration: false,
  isFetching: false,
  error: null,

  // Fetch uncelebrated badges from API
  fetchUncelebrated: async () => {
    const { isFetching } = get();

    // Prevent duplicate requests
    if (isFetching) {
      console.log('🎖️ [CelebrationStore] Already fetching, skipping...');
      return;
    }

    console.log('🎖️ [CelebrationStore] Fetching uncelebrated badges...');
    set({ isFetching: true, error: null });

    try {
      const uncelebrated = await userService.getUncelebratedBadges();
      console.log('🎖️ [CelebrationStore] Received badges:', uncelebrated.length, uncelebrated);

      // Only show modal if we have badges to celebrate
      const hasNewBadges = uncelebrated.length > 0;

      set({
        celebrationQueue: uncelebrated,
        isShowingCelebration: hasNewBadges,
        isFetching: false,
      });

      console.log('🎖️ [CelebrationStore] isShowingCelebration:', hasNewBadges);
    } catch (error) {
      console.error('🎖️ [CelebrationStore] Failed to fetch uncelebrated badges:', error);
      // Keep current queue on error - will retry on next foreground
      set({
        error: error instanceof Error ? error.message : 'Failed to fetch badges',
        isFetching: false,
      });
    }
  },

  // Celebrate the current badge (first in queue) and move to next
  celebrateCurrent: async () => {
    const { celebrationQueue } = get();

    if (celebrationQueue.length === 0) {
      set({ isShowingCelebration: false });
      return;
    }

    const currentBadge = celebrationQueue[0];

    try {
      // Call API to mark badge as celebrated
      await userService.celebrateBadge(currentBadge.id);

      // Remove celebrated badge from queue
      const remainingQueue = celebrationQueue.slice(1);

      // Show modal if more badges remain, otherwise close
      set({
        celebrationQueue: remainingQueue,
        isShowingCelebration: remainingQueue.length > 0,
      });
    } catch (error) {
      console.error('Failed to celebrate badge:', error);
      // Keep badge in queue on error - will retry on dismiss/next foreground
      set({
        error: error instanceof Error ? error.message : 'Failed to celebrate badge',
      });
    }
  },

  // Navigate to trophy case and celebrate current badge
  navigateToTrophyCase: () => {
    const { celebrateCurrent } = get();

    // Celebrate the current badge before navigating
    celebrateCurrent().then(() => {
      // Navigate to profile screen (which contains trophy case)
      router.push('/(tabs)/profile');
    });
  },

  // Reset store (on logout)
  reset: () =>
    set({
      celebrationQueue: [],
      isShowingCelebration: false,
      isFetching: false,
      error: null,
    }),
}));
