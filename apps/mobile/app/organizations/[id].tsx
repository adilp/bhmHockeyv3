import { useEffect, useState, useCallback } from 'react';
import {
  View,
  Text,
  ScrollView,
  StyleSheet,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
  Modal,
  Pressable,
} from 'react-native';
import { useLocalSearchParams, useRouter, Stack, useFocusEffect } from 'expo-router';
import { organizationService } from '@bhmhockey/api-client';
import { useOrganizationStore } from '../../stores/organizationStore';
import { useAuthStore } from '../../stores/authStore';
import { Badge, SkillLevelBadges } from '../../components';
import { colors, spacing, radius } from '../../theme';
import { shareOrganizationInvite } from '../../utils/share';
import type { Organization, OrganizationMember } from '@bhmhockey/shared';

export default function OrganizationDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { user } = useAuthStore();

  const { subscribe, unsubscribe } = useOrganizationStore();

  const [organization, setOrganization] = useState<Organization | null>(null);
  const [members, setMembers] = useState<OrganizationMember[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingMembers, setIsLoadingMembers] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [showMembers, setShowMembers] = useState(true);
  const [selectedMember, setSelectedMember] = useState<OrganizationMember | null>(null);
  const [showMemberActions, setShowMemberActions] = useState(false);

  useFocusEffect(
    useCallback(() => {
      loadOrganization();
    }, [id])
  );

  const loadOrganization = async () => {
    if (!id) return;

    setIsLoading(true);
    try {
      const org = await organizationService.getById(id);
      setOrganization(org);

      if (org.isAdmin) {
        loadMembers();
      }
    } catch (error) {
      Alert.alert('Error', 'Failed to load organization');
      router.back();
    } finally {
      setIsLoading(false);
    }
  };

  const loadMembers = async () => {
    if (!id) return;

    setIsLoadingMembers(true);
    try {
      const membersList = await organizationService.getMembers(id);
      setMembers(membersList);
    } catch (error) {
      console.log('Failed to load members:', error);
    } finally {
      setIsLoadingMembers(false);
    }
  };

  const handleSubscriptionToggle = async () => {
    if (!organization) return;

    if (!user) {
      Alert.alert('Login Required', 'Please log in to subscribe', [
        { text: 'Cancel', style: 'cancel' },
        { text: 'Login', onPress: () => router.push('/(auth)/login') },
      ]);
      return;
    }

    setIsProcessing(true);

    try {
      if (organization.isSubscribed) {
        await unsubscribe(organization.id);
        setOrganization({
          ...organization,
          isSubscribed: false,
          subscriberCount: Math.max(0, organization.subscriberCount - 1),
        });
      } else {
        await subscribe(organization.id);
        setOrganization({
          ...organization,
          isSubscribed: true,
          subscriberCount: organization.subscriberCount + 1,
        });
      }
    } catch (error) {
      Alert.alert('Error', 'Failed to update subscription');
    } finally {
      setIsProcessing(false);
    }
  };

  const handleMemberPress = (member: OrganizationMember) => {
    if (member.id === user?.id) return;
    setSelectedMember(member);
    setShowMemberActions(true);
  };

  const handlePromoteToAdmin = async () => {
    if (!id || !selectedMember) return;

    setShowMemberActions(false);
    setIsProcessing(true);

    try {
      await organizationService.addAdmin(id, { userId: selectedMember.id });
      setMembers(members.map(m =>
        m.id === selectedMember.id ? { ...m, isAdmin: true } : m
      ));
      Alert.alert('Success', `${selectedMember.firstName} is now an admin`);
    } catch (error) {
      Alert.alert('Error', 'Failed to promote member to admin');
    } finally {
      setIsProcessing(false);
      setSelectedMember(null);
    }
  };

  const handleRemoveAdmin = async () => {
    if (!id || !selectedMember) return;

    setShowMemberActions(false);
    setIsProcessing(true);

    try {
      await organizationService.removeAdmin(id, selectedMember.id);
      setMembers(members.map(m =>
        m.id === selectedMember.id ? { ...m, isAdmin: false } : m
      ));
      Alert.alert('Success', `${selectedMember.firstName} is no longer an admin`);
    } catch (error: any) {
      const message = error?.response?.data?.message || 'Failed to remove admin';
      Alert.alert('Error', message);
    } finally {
      setIsProcessing(false);
      setSelectedMember(null);
    }
  };

  const handleRemoveMember = async () => {
    if (!id || !selectedMember) return;

    Alert.alert(
      'Remove Member',
      `Are you sure you want to remove ${selectedMember.firstName} ${selectedMember.lastName} from this organization?`,
      [
        { text: 'Cancel', style: 'cancel', onPress: () => setShowMemberActions(false) },
        {
          text: 'Remove',
          style: 'destructive',
          onPress: async () => {
            setShowMemberActions(false);
            setIsProcessing(true);

            try {
              await organizationService.removeMember(id, selectedMember.id);
              setMembers(members.filter(m => m.id !== selectedMember.id));
              if (organization) {
                setOrganization({
                  ...organization,
                  subscriberCount: Math.max(0, organization.subscriberCount - 1),
                });
              }
              Alert.alert('Success', `${selectedMember.firstName} has been removed`);
            } catch (error) {
              Alert.alert('Error', 'Failed to remove member');
            } finally {
              setIsProcessing(false);
              setSelectedMember(null);
            }
          },
        },
      ]
    );
  };

  const handleShareInvite = async () => {
    if (!organization) return;
    await shareOrganizationInvite(organization.id, organization.name);
  };

  const isAdmin = organization?.isAdmin;

  return (
    <>
      <Stack.Screen
        options={{
          title: organization?.name || 'Organization',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
          headerBackTitle: 'Back',
          headerRight: isAdmin ? () => (
            <TouchableOpacity
              onPress={() => router.push(`/organizations/edit?id=${id}`)}
              style={styles.headerButton}
            >
              <Text style={styles.headerButtonText}>Edit</Text>
            </TouchableOpacity>
          ) : undefined,
        }}
      />

      {isLoading ? (
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
        </View>
      ) : !organization ? (
        <View style={styles.loadingContainer}>
          <Text style={styles.errorText}>Organization not found</Text>
        </View>
      ) : (
        <>
      <ScrollView style={styles.container}>
        {/* Header */}
        <View style={styles.header}>
          <View style={styles.titleRow}>
            <Text style={styles.title}>{organization.name}</Text>
            {isAdmin && <Badge variant="purple">Admin</Badge>}
          </View>

          <SkillLevelBadges levels={organization.skillLevels} />
        </View>

        {/* Description */}
        {organization.description && (
          <View style={styles.section}>
            <Text style={styles.sectionLabel}>About</Text>
            <Text style={styles.sectionText}>{organization.description}</Text>
          </View>
        )}

        {/* Stats */}
        <View style={styles.statsSection}>
          <View style={styles.stat}>
            <Text style={styles.statValue}>{organization.subscriberCount}</Text>
            <Text style={styles.statLabel}>
              {organization.subscriberCount === 1 ? 'Member' : 'Members'}
            </Text>
          </View>
        </View>

        {/* Admin Section - Members List */}
        {isAdmin && (
          <View style={styles.adminSection}>
            <TouchableOpacity
              style={styles.membersHeader}
              onPress={() => setShowMembers(!showMembers)}
            >
              <Text style={styles.membersTitle}>Members</Text>
              <Text style={styles.membersToggle}>{showMembers ? '▲' : '▼'}</Text>
            </TouchableOpacity>

            {showMembers && (
              <View style={styles.membersList}>
                {isLoadingMembers ? (
                  <ActivityIndicator size="small" color={colors.primary.teal} style={{ padding: 20 }} />
                ) : members.length === 0 ? (
                  <Text style={styles.noMembers}>No members yet</Text>
                ) : (
                  members.map((member) => {
                    const isCurrentUser = member.id === user?.id;
                    return (
                      <TouchableOpacity
                        key={member.id}
                        style={styles.memberCard}
                        onPress={() => handleMemberPress(member)}
                        disabled={isCurrentUser}
                        activeOpacity={isCurrentUser ? 1 : 0.7}
                      >
                        <View style={styles.memberAvatar}>
                          <Text style={styles.memberAvatarText}>
                            {member.firstName.charAt(0)}{member.lastName.charAt(0)}
                          </Text>
                        </View>
                        <View style={styles.memberInfo}>
                          <View style={styles.memberNameRow}>
                            <Text style={styles.memberName}>
                              {member.firstName} {member.lastName}
                            </Text>
                            {member.isAdmin && <Badge variant="purple">Admin</Badge>}
                            {isCurrentUser && <Text style={styles.youLabel}>(You)</Text>}
                          </View>
                          <Text style={styles.memberEmail}>{member.email}</Text>
                        </View>
                      </TouchableOpacity>
                    );
                  })
                )}
              </View>
            )}
          </View>
        )}

        {/* Subscribe Button */}
        {user && !isAdmin && (
          <TouchableOpacity
            style={[
              styles.subscribeButton,
              organization.isSubscribed && styles.subscribedButton,
              isProcessing && styles.disabledButton,
            ]}
            onPress={handleSubscriptionToggle}
            disabled={isProcessing}
          >
            {isProcessing ? (
              <ActivityIndicator color={organization.isSubscribed ? colors.primary.teal : colors.bg.darkest} />
            ) : (
              <Text style={[
                styles.subscribeButtonText,
                organization.isSubscribed && styles.subscribedButtonText,
              ]}>
                {organization.isSubscribed ? 'Joined' : 'Join Organization'}
              </Text>
            )}
          </TouchableOpacity>
        )}

        {!isAdmin && (
          <Text style={styles.hint}>
            {organization.isSubscribed
              ? "You'll be notified when new events are posted"
              : 'Join to get notified about new events'}
          </Text>
        )}

        {/* Share Invite Button - visible to all members */}
        {(organization.isSubscribed || isAdmin) && (
          <TouchableOpacity
            style={styles.shareButton}
            onPress={handleShareInvite}
          >
            <Text style={styles.shareButtonText}>Share Invite</Text>
          </TouchableOpacity>
        )}

        <View style={{ height: 40 }} />
      </ScrollView>

      {/* Member Actions Modal */}
      <Modal
        visible={showMemberActions}
        transparent
        animationType="fade"
        onRequestClose={() => {
          setShowMemberActions(false);
          setSelectedMember(null);
        }}
      >
        <Pressable
          style={styles.modalOverlay}
          onPress={() => {
            setShowMemberActions(false);
            setSelectedMember(null);
          }}
        >
          <View style={styles.actionSheet}>
            <View style={styles.actionSheetHandle} />
            <View style={styles.actionSheetHeader}>
              <Text style={styles.actionSheetTitle}>
                {selectedMember?.firstName} {selectedMember?.lastName}
              </Text>
              <Text style={styles.actionSheetSubtitle}>
                {selectedMember?.email}
              </Text>
            </View>

            {selectedMember && !selectedMember.isAdmin && (
              <TouchableOpacity style={styles.actionButton} onPress={handlePromoteToAdmin}>
                <Text style={styles.actionButtonText}>Promote to Admin</Text>
              </TouchableOpacity>
            )}

            {selectedMember?.isAdmin && (
              <TouchableOpacity style={styles.actionButton} onPress={handleRemoveAdmin}>
                <Text style={styles.actionButtonText}>Remove Admin Role</Text>
              </TouchableOpacity>
            )}

            <TouchableOpacity
              style={[styles.actionButton, styles.destructiveButton]}
              onPress={handleRemoveMember}
            >
              <Text style={styles.destructiveButtonText}>Remove from Organization</Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={styles.cancelButton}
              onPress={() => {
                setShowMemberActions(false);
                setSelectedMember(null);
              }}
            >
              <Text style={styles.cancelButtonText}>Cancel</Text>
            </TouchableOpacity>
          </View>
        </Pressable>
      </Modal>
        </>
      )}
    </>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.darkest,
  },
  errorText: {
    color: colors.text.muted,
    fontSize: 16,
  },
  header: {
    backgroundColor: colors.bg.dark,
    padding: spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  titleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    marginBottom: spacing.sm,
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: colors.text.primary,
  },
  skillBadge: {
    alignSelf: 'flex-start',
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: radius.round,
  },
  skillText: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.bg.darkest,
  },
  section: {
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    marginTop: spacing.sm,
  },
  sectionLabel: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.sm,
  },
  sectionText: {
    fontSize: 16,
    color: colors.text.secondary,
    lineHeight: 24,
  },
  statsSection: {
    flexDirection: 'row',
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    marginTop: spacing.sm,
  },
  stat: {
    alignItems: 'center',
    marginRight: spacing.xl,
  },
  statValue: {
    fontSize: 28,
    fontWeight: '700',
    color: colors.primary.teal,
  },
  statLabel: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: spacing.xs,
  },
  adminSection: {
    backgroundColor: colors.bg.dark,
    marginTop: spacing.sm,
  },
  membersHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: spacing.md,
  },
  membersTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
  membersToggle: {
    fontSize: 14,
    color: colors.text.muted,
  },
  membersList: {
    paddingHorizontal: spacing.md,
    paddingBottom: spacing.md,
  },
  noMembers: {
    color: colors.text.muted,
    fontStyle: 'italic',
  },
  memberCard: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  memberAvatar: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: colors.bg.active,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: spacing.md,
  },
  memberAvatarText: {
    fontSize: 14,
    fontWeight: '700',
    color: colors.text.muted,
  },
  memberInfo: {
    flex: 1,
  },
  memberNameRow: {
    flexDirection: 'row',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: spacing.sm,
  },
  memberName: {
    fontSize: 15,
    fontWeight: '500',
    color: colors.text.primary,
  },
  youLabel: {
    fontSize: 13,
    color: colors.text.muted,
    fontStyle: 'italic',
  },
  memberEmail: {
    fontSize: 13,
    color: colors.text.muted,
    marginTop: 2,
  },
  subscribeButton: {
    backgroundColor: colors.primary.teal,
    marginHorizontal: spacing.lg,
    marginTop: spacing.lg,
    paddingVertical: spacing.md,
    borderRadius: radius.md,
    alignItems: 'center',
  },
  subscribedButton: {
    backgroundColor: colors.subtle.teal,
    borderWidth: 1,
    borderColor: colors.primary.teal,
  },
  disabledButton: {
    opacity: 0.7,
  },
  subscribeButtonText: {
    color: colors.bg.darkest,
    fontSize: 18,
    fontWeight: '600',
  },
  subscribedButtonText: {
    color: colors.primary.teal,
  },
  hint: {
    textAlign: 'center',
    color: colors.text.muted,
    fontSize: 14,
    marginTop: spacing.sm,
    marginBottom: spacing.xl,
    paddingHorizontal: spacing.lg,
  },
  shareButton: {
    backgroundColor: colors.bg.hover,
    marginHorizontal: spacing.lg,
    marginTop: spacing.md,
    paddingVertical: spacing.md,
    borderRadius: radius.md,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  shareButtonText: {
    color: colors.text.secondary,
    fontSize: 16,
    fontWeight: '600',
  },
  // Modal styles
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.6)',
    justifyContent: 'flex-end',
  },
  actionSheet: {
    backgroundColor: colors.bg.dark,
    borderTopLeftRadius: radius.xl,
    borderTopRightRadius: radius.xl,
    paddingBottom: 34,
  },
  actionSheetHandle: {
    width: 36,
    height: 4,
    backgroundColor: colors.border.muted,
    borderRadius: radius.round,
    alignSelf: 'center',
    marginTop: spacing.sm,
    marginBottom: spacing.md,
  },
  actionSheetHeader: {
    padding: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
    alignItems: 'center',
  },
  actionSheetTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
  },
  actionSheetSubtitle: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: spacing.xs,
  },
  actionButton: {
    padding: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  actionButtonText: {
    fontSize: 16,
    color: colors.primary.teal,
    textAlign: 'center',
  },
  destructiveButton: {
    borderBottomWidth: 0,
  },
  destructiveButtonText: {
    fontSize: 16,
    color: colors.status.error,
    textAlign: 'center',
  },
  cancelButton: {
    padding: spacing.md,
    marginTop: spacing.sm,
    backgroundColor: colors.bg.hover,
    marginHorizontal: spacing.md,
    borderRadius: radius.md,
  },
  cancelButtonText: {
    fontSize: 16,
    color: colors.text.muted,
    textAlign: 'center',
    fontWeight: '600',
  },
  headerButton: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
  },
  headerButtonText: {
    color: colors.primary.teal,
    fontSize: 16,
    fontWeight: '600',
  },
});
