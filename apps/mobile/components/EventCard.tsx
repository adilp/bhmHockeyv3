import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { colors, spacing, radius } from '../theme';
import { Badge } from './Badge';
import { SkillLevelDots } from './SkillLevelDots';
import { OrgAvatar } from './OrgAvatar';
import type { EventDto, SkillLevel } from '@bhmhockey/shared';

export type EventCardVariant = 'available' | 'registered' | 'waitlisted' | 'organizing';

interface EventCardProps {
  event: EventDto;
  variant: EventCardVariant;
  onPress: () => void;
}

// Accent and dot colors per variant (available has no accent)
const variantColors: Record<EventCardVariant, string | null> = {
  available: null,
  registered: colors.primary.green,
  waitlisted: colors.status.warning,
  organizing: colors.primary.purple,
};

function formatDateTime(dateString: string): { date: string; time: string } {
  const date = new Date(dateString);
  const now = new Date();

  // Get date parts for comparison (in local timezone)
  const eventDay = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const tomorrow = new Date(today);
  tomorrow.setDate(tomorrow.getDate() + 1);

  // Format time
  const timeStr = date.toLocaleTimeString('en-US', {
    hour: 'numeric',
    minute: '2-digit',
  });

  // Check if today or tomorrow
  if (eventDay.getTime() === today.getTime()) {
    return { date: 'Today', time: timeStr };
  }
  if (eventDay.getTime() === tomorrow.getTime()) {
    return { date: 'Tomorrow', time: timeStr };
  }

  // Otherwise show full date
  const dateStr = date.toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
  });
  return { date: dateStr, time: timeStr };
}

function getPaymentBadgeVariant(status?: string): { text: string; variant: 'green' | 'warning' | 'error' } {
  switch (status) {
    case 'Verified':
      return { text: 'Paid', variant: 'green' };
    case 'MarkedPaid':
      return { text: 'Awaiting Verification', variant: 'warning' };
    case 'Pending':
    default:
      return { text: 'Unpaid', variant: 'error' };
  }
}

export function EventCard({ event, variant, onPress }: EventCardProps) {
  const accentColor = variantColors[variant];
  const spotsLeft = event.maxPlayers - event.registeredCount;

  return (
    <TouchableOpacity style={styles.card} onPress={onPress} activeOpacity={0.7}>
      {/* Left accent bar - only show for registered/organizing */}
      {accentColor && <View style={[styles.accent, { backgroundColor: accentColor }]} />}

      <View style={styles.content}>
        {/* Organization row - most prominent, first thing users see */}
        {/* If no org, use event name as title; fallback to "Pickup" */}
        <View style={styles.orgRow}>
          <OrgAvatar name={event.organizationName || event.name || 'Pickup'} size="small" />
          <Text style={styles.orgName} numberOfLines={1}>
            {event.organizationName || event.name || 'Pickup'}
          </Text>
        </View>

        {/* Date and venue */}
        <Text style={styles.dateTime}>
          <Text style={styles.dateText}>{formatDateTime(event.eventDate).date}</Text>
          <Text style={styles.timeText}> at {formatDateTime(event.eventDate).time}</Text>
        </Text>
        {event.venue && (
          <Text style={styles.venue} numberOfLines={1}>{event.venue}</Text>
        )}

        {/* Skill level dots - subtle, under venue */}
        {event.skillLevels && event.skillLevels.length > 0 && (
          <View style={styles.skillDotsRow}>
            <SkillLevelDots levels={event.skillLevels as SkillLevel[]} />
          </View>
        )}

        {/* Event name - only show if org exists (otherwise name is already the title) */}
        {event.organizationName && event.name && (
          <Text style={styles.name} numberOfLines={1}>{event.name}</Text>
        )}

        {/* Footer badges */}
        <View style={styles.footer}>
          {variant === 'available' && (
            <AvailableBadges spotsLeft={spotsLeft} waitlistCount={event.waitlistCount} />
          )}
          {variant === 'registered' && (
            <RegisteredBadges event={event} />
          )}
          {variant === 'waitlisted' && (
            <WaitlistedBadges event={event} />
          )}
          {variant === 'organizing' && (
            <OrganizingBadges event={event} />
          )}
        </View>
      </View>

      {/* Price column */}
      <View style={styles.priceColumn}>
        {event.cost > 0 ? (
          <>
            <Text style={styles.priceAmount}>${event.cost}</Text>
            <Text style={styles.priceLabel}>per player</Text>
          </>
        ) : (
          <Text style={styles.freeLabel}>Free</Text>
        )}
      </View>
    </TouchableOpacity>
  );
}

// Sub-components for different badge configurations
function AvailableBadges({ spotsLeft, waitlistCount }: { spotsLeft: number; waitlistCount: number }) {
  // Show "Full" and waitlist count when no spots
  if (spotsLeft <= 0) {
    return (
      <View style={styles.availableBadges}>
        <Badge variant="error">Full</Badge>
        {waitlistCount > 0 && (
          <Badge variant="warning">{waitlistCount} on waitlist</Badge>
        )}
      </View>
    );
  }

  // Only show badge when 2 or fewer spots remain
  if (spotsLeft <= 2) {
    return (
      <Badge variant="error">
        {spotsLeft} {spotsLeft === 1 ? 'spot' : 'spots'} left
      </Badge>
    );
  }
  return null;
}

function WaitlistedBadges({ event }: { event: EventDto }) {
  const spotsLeft = event.maxPlayers - event.registeredCount;
  const isFull = spotsLeft <= 0;

  // If roster is full, show position (you're actually queued for capacity)
  if (isFull) {
    return (
      <View style={styles.waitlistedStats}>
        <Badge variant="warning">
          #{event.myWaitlistPosition} on waitlist
        </Badge>
      </View>
    );
  }

  // Roster has space - you're on waitlist because of payment (pay-to-play model)
  // Show payment status instead of position
  const { text, variant } = getPaymentBadgeVariant(event.myPaymentStatus);
  return (
    <View style={styles.waitlistedStats}>
      <Badge variant={variant}>{text}</Badge>
    </View>
  );
}

function RegisteredBadges({ event }: { event: EventDto }) {
  const { text, variant } = getPaymentBadgeVariant(event.myPaymentStatus);
  const isBlackTeam = event.myTeamAssignment === 'Black';

  return (
    <View style={styles.registeredStats}>
      {/* Team badge */}
      {event.myTeamAssignment && (
        <View style={[
          styles.teamBadge,
          isBlackTeam ? styles.teamBlack : styles.teamWhite
        ]}>
          <Text style={[
            styles.teamBadgeText,
            isBlackTeam ? styles.teamBlackText : styles.teamWhiteText
          ]}>
            Team {event.myTeamAssignment}
          </Text>
        </View>
      )}
      {/* Payment badge */}
      {event.cost > 0 && <Badge variant={variant}>{text}</Badge>}
    </View>
  );
}

function OrganizingBadges({ event }: { event: EventDto }) {
  return (
    <View style={styles.organizerStats}>
      <Badge variant="purple">{event.registeredCount}/{event.maxPlayers}</Badge>
      {event.cost > 0 && event.unpaidCount !== undefined && event.unpaidCount > 0 && (
        <Badge variant="error">{event.unpaidCount} unpaid</Badge>
      )}
      {event.waitlistCount > 0 && (
        <Badge variant="warning">{event.waitlistCount} waitlist</Badge>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    marginBottom: spacing.sm,
    flexDirection: 'row',
    borderWidth: 1,
    borderColor: colors.border.default,
    overflow: 'hidden',
  },
  accent: {
    width: 4,
  },
  content: {
    flex: 1,
    padding: 14,
  },
  dateTime: {
    fontSize: 16,
    color: colors.text.primary,
    marginBottom: 2,
  },
  dateText: {
    fontWeight: '600',
  },
  timeText: {
    fontWeight: '400',
    color: colors.text.secondary,
  },
  venue: {
    fontSize: 13,
    color: colors.text.secondary,
    marginBottom: spacing.sm,
  },
  orgRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    marginBottom: spacing.sm,
  },
  orgName: {
    fontSize: 14,
    fontWeight: '400',
    color: colors.text.secondary,
    flex: 1,
  },
  name: {
    fontSize: 13,
    fontWeight: '400',
    color: colors.text.subtle,
    marginBottom: spacing.xs,
  },
  skillDotsRow: {
    marginBottom: spacing.sm,
  },
  footer: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
  },
  organizerStats: {
    flexDirection: 'row',
    gap: spacing.sm,
  },
  registeredStats: {
    flexDirection: 'row',
    gap: spacing.sm,
    alignItems: 'center',
  },
  waitlistedStats: {
    flexDirection: 'row',
    gap: spacing.sm,
    alignItems: 'center',
  },
  availableBadges: {
    flexDirection: 'row',
    gap: spacing.sm,
    alignItems: 'center',
  },
  teamBadge: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: radius.sm,
  },
  teamBlack: {
    backgroundColor: colors.bg.darkest,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  teamWhite: {
    backgroundColor: colors.text.primary,
  },
  teamBadgeText: {
    fontSize: 11,
    fontWeight: '700',
  },
  teamBlackText: {
    color: colors.text.primary,
  },
  teamWhiteText: {
    color: colors.bg.darkest,
  },
  priceColumn: {
    justifyContent: 'center',
    alignItems: 'flex-end',
    paddingRight: 14,
    paddingLeft: spacing.sm,
    minWidth: 70,
  },
  priceAmount: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.text.primary,
  },
  priceLabel: {
    fontSize: 10,
    color: colors.text.subtle,
    marginTop: 2,
  },
  freeLabel: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.primary.green,
  },
});
