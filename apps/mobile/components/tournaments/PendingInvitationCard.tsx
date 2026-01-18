import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { formatDistanceToNow } from 'date-fns';
import { colors, spacing, radius } from '../../theme';
import type { PendingTeamInvitationDto } from '@bhmhockey/shared';

interface PendingInvitationCardProps {
  invitation: PendingTeamInvitationDto;
  onAccept: (invitation: PendingTeamInvitationDto) => void;
  onDecline: (invitation: PendingTeamInvitationDto) => void;
  isProcessing?: boolean;
}

export function PendingInvitationCard({
  invitation,
  onAccept,
  onDecline,
  isProcessing = false,
}: PendingInvitationCardProps) {
  // Format relative time
  const relativeTime = formatDistanceToNow(new Date(invitation.invitedAt), {
    addSuffix: true,
  });

  return (
    <View style={styles.card}>
      {/* Tournament icon */}
      <View style={styles.iconContainer}>
        <Text style={styles.iconText}>🏆</Text>
      </View>

      {/* Content */}
      <View style={styles.content}>
        {/* Tournament name - most prominent */}
        <Text style={styles.tournamentName} numberOfLines={1}>
          {invitation.tournamentName}
        </Text>

        {/* Team info */}
        <Text style={styles.infoText}>
          <Text style={styles.infoLabel}>Team: </Text>
          <Text style={styles.infoValue}>{invitation.teamName}</Text>
        </Text>

        {/* Captain info */}
        <Text style={styles.infoText}>
          <Text style={styles.infoLabel}>Captain: </Text>
          <Text style={styles.infoValue}>{invitation.captainName}</Text>
        </Text>

        {/* Invited time */}
        <Text style={styles.timeText}>Invited {relativeTime}</Text>

        {/* Action buttons */}
        <View style={styles.buttonRow}>
          <TouchableOpacity
            style={[styles.button, styles.declineButton]}
            onPress={() => onDecline(invitation)}
            disabled={isProcessing}
            activeOpacity={0.7}
          >
            <Text style={[styles.buttonText, styles.declineButtonText]}>
              Decline
            </Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[styles.button, styles.acceptButton]}
            onPress={() => onAccept(invitation)}
            disabled={isProcessing}
            activeOpacity={0.7}
          >
            <Text style={[styles.buttonText, styles.acceptButtonText]}>
              Accept ✓
            </Text>
          </TouchableOpacity>
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
    marginBottom: spacing.sm,
    flexDirection: 'row',
    padding: 14,
  },
  iconContainer: {
    width: 48,
    height: 48,
    borderRadius: radius.md,
    backgroundColor: colors.bg.elevated,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: spacing.md,
  },
  iconText: {
    fontSize: 24,
  },
  content: {
    flex: 1,
  },
  tournamentName: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: spacing.sm,
  },
  infoText: {
    fontSize: 13,
    marginBottom: spacing.xs,
  },
  infoLabel: {
    color: colors.text.muted,
    fontWeight: '400',
  },
  infoValue: {
    color: colors.text.secondary,
    fontWeight: '500',
  },
  timeText: {
    fontSize: 12,
    color: colors.text.subtle,
    marginTop: spacing.xs,
    marginBottom: spacing.md,
  },
  buttonRow: {
    flexDirection: 'row',
    gap: spacing.sm,
  },
  button: {
    flex: 1,
    paddingVertical: 10,
    borderRadius: radius.md,
    alignItems: 'center',
    justifyContent: 'center',
  },
  declineButton: {
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.muted,
  },
  acceptButton: {
    backgroundColor: colors.primary.teal,
  },
  buttonText: {
    fontSize: 14,
    fontWeight: '600',
  },
  declineButtonText: {
    color: colors.text.secondary,
  },
  acceptButtonText: {
    color: colors.bg.darkest,
  },
});
