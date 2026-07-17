import { useCallback, useMemo, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
  Modal,
} from 'react-native';
import { useLocalSearchParams, useRouter, Stack, useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { organizationService } from '@bhmhockey/api-client';
import type { OrganizationMember, Position } from '@bhmhockey/shared';
import { useOrganizationStore } from '../../../stores/organizationStore';
import { colors, spacing, radius } from '../../../theme';

export default function AutoRosterScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();

  const autoRoster = useOrganizationStore((state) => state.autoRoster);
  const members = useOrganizationStore((state) => state.members);
  const fetchAutoRoster = useOrganizationStore((state) => state.fetchAutoRoster);
  const fetchMembers = useOrganizationStore((state) => state.fetchMembers);
  const addAutoRosterMember = useOrganizationStore((state) => state.addAutoRosterMember);
  const removeAutoRosterMember = useOrganizationStore((state) => state.removeAutoRosterMember);
  const reorderAutoRoster = useOrganizationStore((state) => state.reorderAutoRoster);

  const [isLoading, setIsLoading] = useState(true);
  const [isProcessing, setIsProcessing] = useState(false);
  const [showAddModal, setShowAddModal] = useState(false);

  useFocusEffect(
    useCallback(() => {
      loadData();
    }, [id])
  );

  const loadData = async () => {
    if (!id) return;

    setIsLoading(true);
    try {
      const org = await organizationService.getById(id);
      if (!org.isAdmin) {
        Alert.alert('Access Denied', 'Only organization admins can manage the auto-roster');
        router.back();
        return;
      }
      await Promise.all([fetchAutoRoster(id), fetchMembers(id)]);
    } catch (error) {
      Alert.alert('Error', 'Failed to load auto-roster');
      router.back();
    } finally {
      setIsLoading(false);
    }
  };

  // Members not yet in the auto-roster list
  const availableMembers = useMemo(() => {
    const listedUserIds = new Set(autoRoster.map((m) => m.userId));
    return members.filter((m) => !listedUserIds.has(m.id));
  }, [members, autoRoster]);

  const showStoreErrorOr = (fallback: string) => {
    const message = useOrganizationStore.getState().error || fallback;
    Alert.alert('Error', message);
  };

  const handleAdd = async (member: OrganizationMember, position: Position) => {
    if (!id || isProcessing) return;

    setIsProcessing(true);
    const success = await addAutoRosterMember(id, member.id, position);
    setIsProcessing(false);

    if (!success) {
      showStoreErrorOr('Failed to add player to auto-roster');
    }
  };

  const handleRemove = (userId: string, name: string) => {
    if (!id) return;

    Alert.alert(
      'Remove from Auto-Roster',
      `Remove ${name} from the auto-roster? They will no longer be automatically added to new events.`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Remove',
          style: 'destructive',
          onPress: async () => {
            const success = await removeAutoRosterMember(id, userId);
            if (!success) {
              showStoreErrorOr('Failed to remove player from auto-roster');
            }
          },
        },
      ]
    );
  };

  const handleMove = async (index: number, direction: -1 | 1) => {
    if (!id || isProcessing) return;

    const target = index + direction;
    if (target < 0 || target >= autoRoster.length) return;

    const orderedUserIds = autoRoster.map((m) => m.userId);
    [orderedUserIds[index], orderedUserIds[target]] = [orderedUserIds[target], orderedUserIds[index]];

    setIsProcessing(true);
    const success = await reorderAutoRoster(id, orderedUserIds);
    setIsProcessing(false);

    if (!success) {
      showStoreErrorOr('Failed to reorder auto-roster');
    }
  };

  const positionButtons = (member: OrganizationMember) => {
    const hasGoalie = !!member.positions?.goalie;
    const hasSkater = !!member.positions?.skater;

    // Organizer semantics: either position is allowed regardless of profile,
    // but the member's own position(s) are emphasized as the default choice
    return (
      <View style={styles.positionButtons}>
        <TouchableOpacity
          style={[styles.positionButton, hasSkater && styles.positionButtonDefault]}
          onPress={() => handleAdd(member, 'Skater')}
          disabled={isProcessing}
        >
          <Text
            style={[styles.positionButtonText, hasSkater && styles.positionButtonTextDefault]}
            allowFontScaling={false}
          >
            Skater
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.positionButton, hasGoalie && styles.positionButtonDefault]}
          onPress={() => handleAdd(member, 'Goalie')}
          disabled={isProcessing}
        >
          <Text
            style={[styles.positionButtonText, hasGoalie && styles.positionButtonTextDefault]}
            allowFontScaling={false}
          >
            Goalie
          </Text>
        </TouchableOpacity>
      </View>
    );
  };

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Auto-Roster',
          headerBackTitle: 'Back',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
        }}
      />

      {isLoading ? (
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
        </View>
      ) : (
        <ScrollView style={styles.container}>
          <View style={styles.explanationBox}>
            <Text style={styles.explanationText}>
              Auto-roster players are automatically added to new events for this organization, in
              the order below. Skaters fill the roster first; extras go to the waitlist. Goalies
              are always rostered.
            </Text>
          </View>

          {autoRoster.length === 0 ? (
            <View style={styles.emptyBox}>
              <Text style={styles.emptyText}>No auto-roster players yet</Text>
              <Text style={styles.emptyHint}>
                Add your regulars so they're automatically rostered when you create an event.
              </Text>
            </View>
          ) : (
            <View style={styles.list}>
              {autoRoster.map((member, index) => (
                <View key={member.userId} style={styles.memberRow}>
                  <Text style={styles.orderNumber} allowFontScaling={false}>
                    {index + 1}
                  </Text>
                  <View style={styles.memberInfo}>
                    <Text style={styles.memberName} allowFontScaling={false}>
                      {member.firstName} {member.lastName}
                    </Text>
                    <Text style={styles.memberPosition} allowFontScaling={false}>
                      {member.position}
                    </Text>
                  </View>
                  <View style={styles.rowActions}>
                    <TouchableOpacity
                      style={[styles.iconButton, index === 0 && styles.iconButtonDisabled]}
                      onPress={() => handleMove(index, -1)}
                      disabled={index === 0 || isProcessing}
                    >
                      <Ionicons
                        name="chevron-up"
                        size={20}
                        color={index === 0 ? colors.text.placeholder : colors.text.secondary}
                      />
                    </TouchableOpacity>
                    <TouchableOpacity
                      style={[
                        styles.iconButton,
                        index === autoRoster.length - 1 && styles.iconButtonDisabled,
                      ]}
                      onPress={() => handleMove(index, 1)}
                      disabled={index === autoRoster.length - 1 || isProcessing}
                    >
                      <Ionicons
                        name="chevron-down"
                        size={20}
                        color={
                          index === autoRoster.length - 1
                            ? colors.text.placeholder
                            : colors.text.secondary
                        }
                      />
                    </TouchableOpacity>
                    <TouchableOpacity
                      style={styles.iconButton}
                      onPress={() => handleRemove(member.userId, `${member.firstName} ${member.lastName}`)}
                      disabled={isProcessing}
                    >
                      <Ionicons name="close" size={20} color={colors.status.error} />
                    </TouchableOpacity>
                  </View>
                </View>
              ))}
            </View>
          )}

          <TouchableOpacity style={styles.addButton} onPress={() => setShowAddModal(true)}>
            <Ionicons name="add" size={20} color={colors.bg.darkest} />
            <Text style={styles.addButtonText}>Add Player</Text>
          </TouchableOpacity>

          <View style={{ height: 40 }} />
        </ScrollView>
      )}

      {/* Add Player Modal */}
      <Modal visible={showAddModal} transparent animationType="slide">
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Add Player</Text>
              <TouchableOpacity onPress={() => setShowAddModal(false)}>
                <Text style={styles.modalDone}>Done</Text>
              </TouchableOpacity>
            </View>
            <ScrollView style={styles.modalList}>
              {availableMembers.length === 0 ? (
                <Text style={styles.emptyText}>All members are already in the auto-roster</Text>
              ) : (
                availableMembers.map((member) => (
                  <View key={member.id} style={styles.candidateRow}>
                    <View style={styles.memberInfo}>
                      <Text style={styles.memberName} allowFontScaling={false}>
                        {member.firstName} {member.lastName}
                      </Text>
                      <Text style={styles.memberPosition} allowFontScaling={false}>
                        {[
                          member.positions?.skater ? `Skater (${member.positions.skater})` : null,
                          member.positions?.goalie ? `Goalie (${member.positions.goalie})` : null,
                        ]
                          .filter(Boolean)
                          .join(' · ') || 'No positions set'}
                      </Text>
                    </View>
                    {positionButtons(member)}
                  </View>
                ))
              )}
              <View style={{ height: 20 }} />
            </ScrollView>
          </View>
        </View>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
    padding: spacing.md,
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.darkest,
  },
  explanationBox: {
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderRadius: radius.md,
    marginBottom: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  explanationText: {
    fontSize: 14,
    color: colors.text.secondary,
    lineHeight: 20,
  },
  emptyBox: {
    alignItems: 'center',
    padding: spacing.lg,
  },
  emptyText: {
    fontSize: 15,
    color: colors.text.muted,
    textAlign: 'center',
  },
  emptyHint: {
    fontSize: 13,
    color: colors.text.subtle,
    textAlign: 'center',
    marginTop: spacing.xs,
  },
  list: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  memberRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border.muted,
  },
  orderNumber: {
    width: 24,
    fontSize: 14,
    fontWeight: '700',
    color: colors.text.muted,
  },
  memberInfo: {
    flex: 1,
  },
  memberName: {
    fontSize: 15,
    fontWeight: '500',
    color: colors.text.primary,
  },
  memberPosition: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: 2,
  },
  rowActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.xs,
  },
  iconButton: {
    padding: spacing.sm,
    borderRadius: radius.sm,
    backgroundColor: colors.bg.elevated,
  },
  iconButtonDisabled: {
    opacity: 0.5,
  },
  addButton: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    padding: spacing.md,
    alignItems: 'center',
    justifyContent: 'center',
    flexDirection: 'row',
    gap: spacing.sm,
    marginTop: spacing.lg,
  },
  addButtonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
  },
  positionButtons: {
    flexDirection: 'row',
    gap: spacing.sm,
  },
  positionButton: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.muted,
    backgroundColor: colors.bg.elevated,
  },
  positionButtonDefault: {
    borderColor: colors.primary.teal,
    backgroundColor: colors.subtle.teal,
  },
  positionButtonText: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.muted,
  },
  positionButtonTextDefault: {
    color: colors.primary.teal,
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.7)',
    justifyContent: 'flex-end',
  },
  modalContent: {
    backgroundColor: colors.bg.dark,
    borderTopLeftRadius: radius.xl,
    borderTopRightRadius: radius.xl,
    paddingBottom: 34,
    borderWidth: 1,
    borderColor: colors.border.default,
    borderBottomWidth: 0,
    maxHeight: '70%',
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
  },
  modalDone: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.primary.teal,
  },
  modalList: {
    padding: spacing.md,
  },
  candidateRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: spacing.sm,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border.muted,
  },
});
