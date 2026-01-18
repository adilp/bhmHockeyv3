import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  TouchableWithoutFeedback,
  ScrollView,
  ActivityIndicator,
} from 'react-native';
import type { TournamentTeamMemberDto } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../../theme';

interface TransferCaptainModalProps {
  visible: boolean;
  onClose: () => void;
  onTransfer: (newCaptainUserId: string) => void;
  members: TournamentTeamMemberDto[];
  currentCaptainId: string;
  isProcessing?: boolean;
}

/**
 * TransferCaptainModal - Modal for transferring captaincy to another team member
 *
 * Features:
 * - List of eligible members (Status = "Accepted" only, excluding current captain)
 * - Tap to select, then confirm button
 * - Warning text about transferring captain role
 * - Cancel and Confirm buttons
 * - Disabled UI when processing
 */
export function TransferCaptainModal({
  visible,
  onClose,
  onTransfer,
  members,
  currentCaptainId,
  isProcessing = false,
}: TransferCaptainModalProps) {
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);

  // Filter to only accepted members who aren't the current captain
  const eligibleMembers = members.filter(
    (m) => m.status === 'Accepted' && m.userId !== currentCaptainId
  );

  const handleConfirm = () => {
    if (selectedUserId) {
      onTransfer(selectedUserId);
    }
  };

  const handleClose = () => {
    setSelectedUserId(null);
    onClose();
  };

  const selectedMember = eligibleMembers.find((m) => m.userId === selectedUserId);

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={handleClose}
    >
      <TouchableWithoutFeedback onPress={handleClose}>
        <View style={styles.overlay}>
          <TouchableWithoutFeedback>
            <View style={styles.modal}>
              {/* Header */}
              <View style={styles.header}>
                <Text style={styles.title} allowFontScaling={false}>
                  Transfer Captain Role
                </Text>
                <Text style={styles.subtitle} allowFontScaling={false}>
                  Select a team member to become the new captain
                </Text>
              </View>

              {/* Member List */}
              <ScrollView
                style={styles.scrollView}
                contentContainerStyle={styles.scrollContent}
                showsVerticalScrollIndicator={false}
              >
                {eligibleMembers.length === 0 ? (
                  <View style={styles.emptyState}>
                    <Text style={styles.emptyText} allowFontScaling={false}>
                      No eligible members available
                    </Text>
                  </View>
                ) : (
                  eligibleMembers.map((member) => (
                    <TouchableOpacity
                      key={member.id}
                      style={[
                        styles.memberRow,
                        selectedUserId === member.userId && styles.memberRowSelected,
                      ]}
                      onPress={() => setSelectedUserId(member.userId)}
                      disabled={isProcessing}
                    >
                      <View style={styles.memberInfo}>
                        <Text
                          style={[
                            styles.memberName,
                            selectedUserId === member.userId && styles.memberNameSelected,
                          ]}
                          allowFontScaling={false}
                        >
                          {member.userFirstName} {member.userLastName}
                        </Text>
                        {member.position && (
                          <Text style={styles.memberPosition} allowFontScaling={false}>
                            {member.position}
                          </Text>
                        )}
                      </View>
                      {selectedUserId === member.userId && (
                        <View style={styles.checkmark}>
                          <Text style={styles.checkmarkText} allowFontScaling={false}>
                            ✓
                          </Text>
                        </View>
                      )}
                    </TouchableOpacity>
                  ))
                )}
              </ScrollView>

              {/* Warning */}
              {selectedMember && (
                <View style={styles.warning}>
                  <Text style={styles.warningText} allowFontScaling={false}>
                    This will transfer the captain role to {selectedMember.userFirstName}{' '}
                    {selectedMember.userLastName}. You will become a regular player.
                  </Text>
                </View>
              )}

              {/* Actions */}
              <View style={styles.actions}>
                <TouchableOpacity
                  style={[
                    styles.confirmButton,
                    (!selectedUserId || isProcessing) && styles.confirmButtonDisabled,
                  ]}
                  onPress={handleConfirm}
                  disabled={!selectedUserId || isProcessing}
                >
                  {isProcessing ? (
                    <ActivityIndicator size="small" color={colors.text.primary} />
                  ) : (
                    <Text style={styles.confirmButtonText} allowFontScaling={false}>
                      Confirm Transfer
                    </Text>
                  )}
                </TouchableOpacity>

                <TouchableOpacity
                  style={styles.cancelButton}
                  onPress={handleClose}
                  disabled={isProcessing}
                >
                  <Text style={styles.cancelButtonText} allowFontScaling={false}>
                    Cancel
                  </Text>
                </TouchableOpacity>
              </View>
            </View>
          </TouchableWithoutFeedback>
        </View>
      </TouchableWithoutFeedback>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.7)',
    justifyContent: 'flex-end',
  },
  modal: {
    backgroundColor: colors.bg.dark,
    borderTopLeftRadius: radius.xl,
    borderTopRightRadius: radius.xl,
    maxHeight: '80%',
    paddingBottom: spacing.xl + 20, // Extra padding for home indicator
  },
  header: {
    padding: spacing.lg,
    paddingBottom: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  title: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.text.primary,
    marginBottom: spacing.xs,
  },
  subtitle: {
    fontSize: 14,
    color: colors.text.muted,
  },
  scrollView: {
    flexGrow: 0,
    maxHeight: 300,
  },
  scrollContent: {
    padding: spacing.lg,
    paddingTop: spacing.md,
  },
  emptyState: {
    padding: spacing.xl,
    alignItems: 'center',
  },
  emptyText: {
    fontSize: 14,
    color: colors.text.muted,
  },
  memberRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: colors.bg.elevated,
    padding: spacing.md,
    borderRadius: radius.md,
    marginBottom: spacing.sm,
    borderWidth: 2,
    borderColor: 'transparent',
  },
  memberRowSelected: {
    borderColor: colors.primary.teal,
    backgroundColor: colors.subtle.teal,
  },
  memberInfo: {
    flex: 1,
  },
  memberName: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
  memberNameSelected: {
    color: colors.primary.teal,
  },
  memberPosition: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: 2,
  },
  checkmark: {
    width: 24,
    height: 24,
    borderRadius: 12,
    backgroundColor: colors.primary.teal,
    alignItems: 'center',
    justifyContent: 'center',
    marginLeft: spacing.sm,
  },
  checkmarkText: {
    fontSize: 14,
    color: colors.text.primary,
    fontWeight: '700',
  },
  warning: {
    backgroundColor: colors.status.warningSubtle,
    borderWidth: 1,
    borderColor: colors.status.warning,
    padding: spacing.md,
    marginHorizontal: spacing.lg,
    marginBottom: spacing.md,
    borderRadius: radius.md,
  },
  warningText: {
    fontSize: 13,
    color: colors.status.warning,
    textAlign: 'center',
  },
  actions: {
    paddingHorizontal: spacing.lg,
    gap: spacing.sm,
  },
  confirmButton: {
    backgroundColor: colors.primary.teal,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    borderRadius: radius.md,
    alignItems: 'center',
    minHeight: 48,
    justifyContent: 'center',
  },
  confirmButtonDisabled: {
    backgroundColor: colors.bg.elevated,
    opacity: 0.5,
  },
  confirmButtonText: {
    color: colors.text.primary,
    fontSize: 16,
    fontWeight: '600',
  },
  cancelButton: {
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    alignItems: 'center',
  },
  cancelButtonText: {
    color: colors.text.muted,
    fontSize: 16,
    fontWeight: '600',
  },
});
