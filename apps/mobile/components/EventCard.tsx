import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { colors, spacing, radius } from '../theme';
import { Badge } from './Badge';
import { SkillLevelBadges } from './SkillLevelBadges';
import type { EventDto, SkillLevel } from '@bhmhockey/shared';

export type EventCardVariant = 'available' | 'registered' | 'organizing';

interface EventCardProps {
  event: EventDto;
  variant: EventCardVariant;
  onPress: () => void;
}

// Accent and dot colors per variant (available has no accent)
const variantColors: Record<EventCardVariant, string | null> = {
  available: null,
  registered: colors.primary.green,
  organizing: colors.primary.purple,
};

function formatDateTime(dateString: string): string {
  const date = new Date(dateString);
  return date.toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
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
        {/* Date and venue - most important after cost */}
        <Text style={styles.dateTime}>{formatDateTime(event.eventDate)}</Text>
        {event.venue && (
          <Text style={styles.venue} numberOfLines={1}>{event.venue}</Text>
        )}

        {/* Organization row with colored dot (only for registered/organizing) */}
        <View style={styles.orgRow}>
          {accentColor && <View style={[styles.orgDot, { backgroundColor: accentColor }]} />}
          <Text style={styles.orgName} numberOfLines={1}>
            {event.organizationName || 'Pickup'}
          </Text>
        </View>

        {/* Event name - only show if provided */}
        {event.name && (
          <Text style={styles.name} numberOfLines={1}>{event.name}</Text>
        )}

        {/* Skill level badges */}
        {event.skillLevels && event.skillLevels.length > 0 && (
          <View style={styles.skillLevels}>
            <SkillLevelBadges levels={event.skillLevels as SkillLevel[]} size="small" />
          </View>
        )}

        {/* Footer badges */}
        <View style={styles.footer}>
          {variant === 'available' && (
            <AvailableBadges spotsLeft={spotsLeft} />
          )}
          {variant === 'registered' && (
            <RegisteredBadges event={event} />
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
function AvailableBadges({ spotsLeft }: { spotsLeft: number }) {
  // Only show badge when 2 or fewer spots remain
  if (spotsLeft <= 2 && spotsLeft > 0) {
    return (
      <Badge variant="error">
        {spotsLeft} {spotsLeft === 1 ? 'spot' : 'spots'} left
      </Badge>
    );
  }
  return null;
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
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: 2,
  },
  venue: {
    fontSize: 13,
    color: colors.text.secondary,
    marginBottom: spacing.sm,
  },
  orgRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 4,
  },
  orgDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
    marginRight: spacing.sm,
  },
  orgName: {
    fontSize: 14,
    fontWeight: '500',
    color: colors.text.muted,
  },
  name: {
    fontSize: 13,
    fontWeight: '400',
    color: colors.text.subtle,
    marginBottom: spacing.xs,
  },
  skillLevels: {
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
