import { useCelebrationStore } from '../stores/celebrationStore';
import type { UncelebratedBadgeDto } from '@bhmhockey/shared';

interface UseBadgeCelebration {
  currentBadge: UncelebratedBadgeDto | null;
  remaining: number;
  dismiss: () => void;
  navigateToTrophyCase: () => void;
}

/**
 * Hook for consuming badge celebration state
 *
 * Returns:
 * - currentBadge: The badge currently being celebrated (first in queue)
 * - remaining: How many badges are left after this one
 * - dismiss: Marks current badge as celebrated and shows next (or closes modal)
 * - navigateToTrophyCase: Marks current badge as celebrated and navigates to profile
 */
export function useBadgeCelebration(): UseBadgeCelebration {
  const celebrationQueue = useCelebrationStore((state) => state.celebrationQueue);
  const celebrateCurrent = useCelebrationStore((state) => state.celebrateCurrent);
  const navigateToTrophyCase = useCelebrationStore((state) => state.navigateToTrophyCase);

  // Current badge is first in queue
  const currentBadge = celebrationQueue.length > 0 ? celebrationQueue[0] : null;

  // Remaining is all badges except the current one
  const remaining = celebrationQueue.length > 1 ? celebrationQueue.length - 1 : 0;

  return {
    currentBadge,
    remaining,
    dismiss: celebrateCurrent,
    navigateToTrophyCase,
  };
}
