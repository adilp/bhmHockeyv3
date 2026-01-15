import { Badge } from './Badge';
import type { BadgeVariant } from './Badge';
import type { TournamentStatus } from '@bhmhockey/shared';

interface TournamentStatusBadgeProps {
  status: TournamentStatus;
}

// Map tournament status to badge variant and display text
// Only statuses visible to regular users are handled:
// - Open = teal
// - InProgress = green
// - Completed = default (gray)
// - Cancelled = error (red)
// Draft, RegistrationClosed, Postponed should not be shown to regular users
const statusConfig: Record<TournamentStatus, { variant: BadgeVariant; label: string }> = {
  Draft: { variant: 'default', label: 'Draft' },
  Open: { variant: 'teal', label: 'Open' },
  RegistrationClosed: { variant: 'default', label: 'Registration Closed' },
  InProgress: { variant: 'green', label: 'In Progress' },
  Completed: { variant: 'default', label: 'Completed' },
  Postponed: { variant: 'warning', label: 'Postponed' },
  Cancelled: { variant: 'error', label: 'Cancelled' },
};

export function TournamentStatusBadge({ status }: TournamentStatusBadgeProps) {
  const config = statusConfig[status] ?? statusConfig.Completed;

  return (
    <Badge variant={config.variant}>
      {config.label}
    </Badge>
  );
}
