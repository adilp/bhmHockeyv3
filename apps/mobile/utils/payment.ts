import type { PaymentStatus } from '@bhmhockey/shared';
import type { BadgeVariant } from '../components/Badge';

export interface PaymentBadgeInfo {
  text: string;
  variant: BadgeVariant;
}

/**
 * Returns display text and Badge variant for a payment status.
 *
 * Usage with Badge component:
 *   const info = getPaymentBadgeInfo(status);
 *   <Badge variant={info.variant}>{info.text}</Badge>
 */
export function getPaymentBadgeInfo(status?: PaymentStatus | null): PaymentBadgeInfo {
  switch (status) {
    case 'Verified':
      return { text: 'Paid', variant: 'green' };
    case 'MarkedPaid':
      return { text: 'Awaiting', variant: 'warning' };
    case 'Pending':
    default:
      return { text: 'Unpaid', variant: 'error' };
  }
}
