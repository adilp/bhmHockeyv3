import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { colors, spacing, radius } from '../theme';
import { Badge } from './Badge';
import type { EventDto } from '@bhmhockey/shared';

export type EventCardVariant = 'available' | 'registered' | 'organizing';

interface EventCardProps {
  event: EventDto;
  variant: EventCardVariant;
  onPress: () => void;
}

// Accent and dot colors per variant
const variantColors: Record<EventCardVariant, string> = {
  available: colors.primary.teal,
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
      {/* Left accent bar */}
      <View style={[styles.accent, { backgroundColor: accentColor }]} />

      <View style={styles.content}>
        {/* Event name */}
        <Text style={styles.name} numberOfLines={1}>{event.name}</Text>

        {/* Organization row with colored dot */}
        <View style={styles.orgRow}>
          <View style={[styles.orgDot, { backgroundColor: accentColor }]} />
          <Text style={styles.orgName} numberOfLines={1}>
            {event.organizationName || 'Open Game'}
          </Text>
        </View>

        {/* Date and venue */}
        <View style={styles.detailsRow}>
          <Text style={styles.dateTime}>{formatDateTime(event.eventDate)}</Text>
          {event.venue && (
            <Text style={styles.venue} numberOfLines={1}> · {event.venue}</Text>
          )}
        </View>

        {/* Footer badges */}
        <View style={styles.footer}>
          {variant === 'available' && (
            <AvailableBadges spotsLeft={spotsLeft} />
          )}
          {variant === 'registered' && event.cost > 0 && (
            <RegisteredBadges paymentStatus={event.myPaymentStatus} />
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
  if (spotsLeft <= 2 && spotsLeft > 0) {
    return (
      <Badge variant="error">
        {spotsLeft} {spotsLeft === 1 ? 'spot' : 'spots'} left
      </Badge>
    );
  }
  return <Badge variant="teal">{spotsLeft} spots</Badge>;
}

function RegisteredBadges({ paymentStatus }: { paymentStatus?: string }) {
  const { text, variant } = getPaymentBadgeVariant(paymentStatus);
  return <Badge variant={variant}>{text}</Badge>;
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
  name: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: 6,
  },
  orgRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: spacing.sm,
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
    color: colors.text.secondary,
  },
  detailsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 10,
  },
  dateTime: {
    fontSize: 13,
    color: colors.text.muted,
  },
  venue: {
    fontSize: 13,
    color: colors.text.subtle,
    flex: 1,
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
