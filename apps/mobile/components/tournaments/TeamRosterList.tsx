import React from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  ActivityIndicator,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import type { TournamentTeamMemberDto } from '@bhmhockey/shared';
import { Badge } from '../Badge';
import { colors, spacing, radius } from '../../theme';

interface TeamRosterListProps {
  members: TournamentTeamMemberDto[];
  captainUserId?: string;
  currentUserId?: string;
  isCaptain: boolean;
  onRemovePlayer?: (userId: string) => void;
  isLoading?: boolean;
}

// Status badge variant mapping
const getStatusBadgeVariant = (status: TournamentTeamMemberDto['status']): 'green' | 'warning' | 'error' => {
  switch (status) {
    case 'Accepted':
      return 'green';
    case 'Pending':
      return 'warning';
    case 'Declined':
      return 'error';
    default:
      return 'warning';
  }
};

// Status label mapping
const getStatusLabel = (status: TournamentTeamMemberDto['status']): string => {
  switch (status) {
    case 'Accepted':
      return 'Accepted';
    case 'Pending':
      return 'Pending';
    case 'Declined':
      return 'Declined';
    default:
      return status;
  }
};

interface RosterItemProps {
  member: TournamentTeamMemberDto;
  isCaptain: boolean;
  isCurrentUser: boolean;
  isCaptainRole: boolean;
  canRemove: boolean;
  onRemove?: () => void;
}

function RosterItem({ member, isCaptain, isCurrentUser, isCaptainRole, canRemove, onRemove }: RosterItemProps) {
  const fullName = `${member.userFirstName} ${member.userLastName}`;
  const statusVariant = getStatusBadgeVariant(member.status);
  const statusLabel = getStatusLabel(member.status);

  return (
    <View style={styles.memberRow}>
      <View style={styles.memberInfo}>
        {/* Name and captain badge */}
        <View style={styles.nameRow}>
          <Text style={styles.memberName} numberOfLines={1}>
            {fullName}
            {isCurrentUser && <Text style={styles.youLabel}> (You)</Text>}
          </Text>
          {isCaptainRole && (
            <View style={styles.captainBadge}>
              <Ionicons name="star" size={12} color={colors.status.warning} />
              <Text style={styles.captainText}>Captain</Text>
            </View>
          )}
        </View>

        {/* Email and status row */}
        <View style={styles.detailsRow}>
          <Text style={styles.memberEmail} numberOfLines={1}>
            {member.userEmail}
          </Text>
          <Badge variant={statusVariant}>{statusLabel}</Badge>
        </View>
      </View>

      {/* Remove button - only show for captain, for non-captain players with Accepted/Pending status */}
      {canRemove && isCaptain && onRemove && (
        <TouchableOpacity
          style={styles.removeButton}
          onPress={onRemove}
          hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
        >
          <Ionicons name="close-circle" size={24} color={colors.status.error} />
        </TouchableOpacity>
      )}
    </View>
  );
}

export function TeamRosterList({
  members,
  captainUserId,
  currentUserId,
  isCaptain,
  onRemovePlayer,
  isLoading = false,
}: TeamRosterListProps) {
  // Show skeleton loader when loading
  if (isLoading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
      </View>
    );
  }

  // Show empty state when no members
  if (members.length === 0) {
    return (
      <View style={styles.emptyContainer}>
        <Text style={styles.emptyIcon}>👥</Text>
        <Text style={styles.emptyTitle}>No Team Members</Text>
        <Text style={styles.emptyMessage}>Add players to build your team</Text>
      </View>
    );
  }

  const renderItem = ({ item }: { item: TournamentTeamMemberDto }) => {
    const isCaptainRole = item.role === 'Captain' || item.userId === captainUserId;
    const isCurrentUser = item.userId === currentUserId;

    // Can remove if:
    // - User is captain
    // - Target is not captain
    // - Status is Accepted or Pending
    const canRemove =
      isCaptain &&
      !isCaptainRole &&
      (item.status === 'Accepted' || item.status === 'Pending');

    const handleRemove = () => {
      if (onRemovePlayer) {
        onRemovePlayer(item.userId);
      }
    };

    return (
      <RosterItem
        member={item}
        isCaptain={isCaptain}
        isCurrentUser={isCurrentUser}
        isCaptainRole={isCaptainRole}
        canRemove={canRemove}
        onRemove={handleRemove}
      />
    );
  };

  return (
    <FlatList
      data={members}
      renderItem={renderItem}
      keyExtractor={(item) => item.id}
      contentContainerStyle={styles.listContent}
      ItemSeparatorComponent={() => <View style={styles.separator} />}
      scrollEnabled={false} // Disable scroll since it's typically inside a parent ScrollView
    />
  );
}

const styles = StyleSheet.create({
  loadingContainer: {
    paddingVertical: spacing.xl,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  emptyContainer: {
    paddingVertical: spacing.xl,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  emptyIcon: {
    fontSize: 48,
    marginBottom: spacing.sm,
  },
  emptyTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: spacing.xs,
  },
  emptyMessage: {
    fontSize: 14,
    color: colors.text.muted,
    textAlign: 'center',
  },
  listContent: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
    padding: spacing.md,
  },
  memberRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: spacing.sm,
  },
  memberInfo: {
    flex: 1,
    marginRight: spacing.sm,
  },
  nameRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: spacing.xs,
    flexWrap: 'wrap',
    gap: spacing.xs,
  },
  memberName: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.text.primary,
  },
  youLabel: {
    fontSize: 13,
    fontWeight: '400',
    color: colors.text.muted,
  },
  captainBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.status.warningSubtle,
    paddingHorizontal: spacing.sm,
    paddingVertical: 2,
    borderRadius: radius.sm,
    gap: 4,
  },
  captainText: {
    fontSize: 11,
    fontWeight: '600',
    color: colors.status.warning,
  },
  detailsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    flexWrap: 'wrap',
  },
  memberEmail: {
    fontSize: 13,
    color: colors.text.secondary,
    flex: 1,
    marginRight: spacing.xs,
  },
  removeButton: {
    padding: spacing.xs,
  },
  separator: {
    height: 1,
    backgroundColor: colors.border.default,
    marginVertical: spacing.xs,
  },
});
