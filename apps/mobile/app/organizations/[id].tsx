import { useEffect, useState } from 'react';
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
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { organizationService } from '@bhmhockey/api-client';
import { useOrganizationStore } from '../../stores/organizationStore';
import { useAuthStore } from '../../stores/authStore';
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
  const [showMembers, setShowMembers] = useState(false);
  const [selectedMember, setSelectedMember] = useState<OrganizationMember | null>(null);
  const [showMemberActions, setShowMemberActions] = useState(false);

  useEffect(() => {
    loadOrganization();
  }, [id]);

  const loadOrganization = async () => {
    if (!id) return;

    setIsLoading(true);
    try {
      const org = await organizationService.getById(id);
      setOrganization(org);

      // If user is admin, load members automatically
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
    // Don't allow actions on yourself
    if (member.id === user?.id) {
      return;
    }
    setSelectedMember(member);
    setShowMemberActions(true);
  };

  const handlePromoteToAdmin = async () => {
    if (!id || !selectedMember) return;

    setShowMemberActions(false);
    setIsProcessing(true);

    try {
      await organizationService.addAdmin(id, { userId: selectedMember.id });
      // Update local state
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
      // Update local state
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
              // Update local state
              setMembers(members.filter(m => m.id !== selectedMember.id));
              // Update subscriber count
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

  if (isLoading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#003366" />
      </View>
    );
  }

  if (!organization) {
    return (
      <View style={styles.errorContainer}>
        <Text>Organization not found</Text>
      </View>
    );
  }

  const isAdmin = organization.isAdmin;

  return (
    <>
      <Stack.Screen options={{ title: organization.name }} />

      <ScrollView style={styles.container}>
        <View style={styles.header}>
          <View style={styles.titleRow}>
            <Text style={styles.title}>{organization.name}</Text>
            {isAdmin && (
              <View style={styles.adminBadge}>
                <Text style={styles.adminBadgeText}>Admin</Text>
              </View>
            )}
          </View>

          {organization.skillLevel && (
            <View style={[styles.skillBadge, getSkillBadgeStyle(organization.skillLevel)]}>
              <Text style={styles.skillText}>{organization.skillLevel}</Text>
            </View>
          )}
        </View>

        {organization.location && (
          <View style={styles.section}>
            <Text style={styles.sectionLabel}>Location</Text>
            <Text style={styles.location}>{organization.location}</Text>
          </View>
        )}

        {organization.description && (
          <View style={styles.section}>
            <Text style={styles.sectionLabel}>About</Text>
            <Text style={styles.description}>{organization.description}</Text>
          </View>
        )}

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
                  <ActivityIndicator size="small" color="#003366" style={{ padding: 20 }} />
                ) : members.length === 0 ? (
                  <Text style={styles.noMembers}>No members yet</Text>
                ) : (
                  members.map((member) => {
                    const isCurrentUser = member.id === user?.id;
                    return (
                      <TouchableOpacity
                        key={member.id}
                        style={[styles.memberCard, !isCurrentUser && styles.memberCardTappable]}
                        onPress={() => handleMemberPress(member)}
                        disabled={isCurrentUser}
                        activeOpacity={isCurrentUser ? 1 : 0.7}
                      >
                        <View style={styles.memberInfo}>
                          <View style={styles.memberNameRow}>
                            <Text style={styles.memberName}>
                              {member.firstName} {member.lastName}
                            </Text>
                            {member.isAdmin && (
                              <View style={styles.memberAdminBadge}>
                                <Text style={styles.memberAdminBadgeText}>Admin</Text>
                              </View>
                            )}
                            {isCurrentUser && (
                              <Text style={styles.youLabel}>(You)</Text>
                            )}
                          </View>
                          <Text style={styles.memberEmail}>{member.email}</Text>
                        </View>
                        <View style={styles.memberDetails}>
                          {member.skillLevel && (
                            <Text style={styles.memberDetail}>{member.skillLevel}</Text>
                          )}
                          {member.position && (
                            <Text style={styles.memberDetail}>{member.position}</Text>
                          )}
                        </View>
                      </TouchableOpacity>
                    );
                  })
                )}
              </View>
            )}
          </View>
        )}

        {/* Subscribe Button - Only show if not admin */}
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
              <ActivityIndicator color={organization.isSubscribed ? '#003366' : '#FFFFFF'} />
            ) : (
              <Text
                style={[
                  styles.subscribeButtonText,
                  organization.isSubscribed && styles.subscribedButtonText,
                ]}
              >
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
            <View style={styles.actionSheetHeader}>
              <Text style={styles.actionSheetTitle}>
                {selectedMember?.firstName} {selectedMember?.lastName}
              </Text>
              <Text style={styles.actionSheetSubtitle}>
                {selectedMember?.email}
              </Text>
            </View>

            {selectedMember && !selectedMember.isAdmin && (
              <TouchableOpacity
                style={styles.actionButton}
                onPress={handlePromoteToAdmin}
              >
                <Text style={styles.actionButtonText}>Promote to Admin</Text>
              </TouchableOpacity>
            )}

            {selectedMember?.isAdmin && (
              <TouchableOpacity
                style={styles.actionButton}
                onPress={handleRemoveAdmin}
              >
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
  );
}

function getSkillBadgeStyle(skillLevel: string) {
  const colors: Record<string, string> = {
    Gold: '#FFD700',
    Silver: '#C0C0C0',
    Bronze: '#CD7F32',
    'D-League': '#4A90D9',
  };
  return { backgroundColor: colors[skillLevel] || '#666' };
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#FFFFFF',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  errorContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  header: {
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#EEE',
  },
  titleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 12,
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    marginRight: 12,
  },
  adminBadge: {
    backgroundColor: '#003366',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 4,
  },
  adminBadgeText: {
    color: '#fff',
    fontSize: 12,
    fontWeight: '600',
  },
  skillBadge: {
    alignSelf: 'flex-start',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 16,
  },
  skillText: {
    fontSize: 14,
    fontWeight: '600',
  },
  section: {
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#EEE',
  },
  sectionLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: '#888',
    textTransform: 'uppercase',
    marginBottom: 8,
  },
  location: {
    fontSize: 16,
    color: '#333',
  },
  description: {
    fontSize: 16,
    color: '#333',
    lineHeight: 24,
  },
  statsSection: {
    flexDirection: 'row',
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#EEE',
  },
  stat: {
    alignItems: 'center',
    marginRight: 40,
  },
  statValue: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#003366',
  },
  statLabel: {
    fontSize: 14,
    color: '#666',
    marginTop: 4,
  },
  adminSection: {
    borderBottomWidth: 1,
    borderBottomColor: '#EEE',
  },
  membersHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
  },
  membersTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
  },
  membersToggle: {
    fontSize: 14,
    color: '#666',
  },
  membersList: {
    paddingHorizontal: 20,
    paddingBottom: 20,
  },
  noMembers: {
    color: '#888',
    fontStyle: 'italic',
  },
  memberCard: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
  },
  memberCardTappable: {
    backgroundColor: '#fff',
  },
  memberInfo: {
    flex: 1,
  },
  memberNameRow: {
    flexDirection: 'row',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: 8,
  },
  memberName: {
    fontSize: 16,
    fontWeight: '500',
    color: '#333',
  },
  memberAdminBadge: {
    backgroundColor: '#003366',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  memberAdminBadgeText: {
    color: '#fff',
    fontSize: 10,
    fontWeight: '600',
  },
  youLabel: {
    fontSize: 14,
    color: '#888',
    fontStyle: 'italic',
  },
  memberEmail: {
    fontSize: 14,
    color: '#666',
    marginTop: 2,
  },
  memberDetails: {
    flexDirection: 'row',
    gap: 8,
  },
  memberDetail: {
    fontSize: 12,
    color: '#888',
    backgroundColor: '#f5f5f5',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 4,
  },
  subscribeButton: {
    backgroundColor: '#003366',
    marginHorizontal: 20,
    marginTop: 24,
    paddingVertical: 16,
    borderRadius: 12,
    alignItems: 'center',
  },
  subscribedButton: {
    backgroundColor: '#E8F4FF',
    borderWidth: 2,
    borderColor: '#003366',
  },
  disabledButton: {
    opacity: 0.7,
  },
  subscribeButtonText: {
    color: '#FFFFFF',
    fontSize: 18,
    fontWeight: '600',
  },
  subscribedButtonText: {
    color: '#003366',
  },
  hint: {
    textAlign: 'center',
    color: '#888',
    fontSize: 14,
    marginTop: 12,
    marginBottom: 40,
    paddingHorizontal: 20,
  },
  // Modal styles
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'flex-end',
  },
  actionSheet: {
    backgroundColor: '#fff',
    borderTopLeftRadius: 16,
    borderTopRightRadius: 16,
    paddingBottom: 34,
  },
  actionSheetHeader: {
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
    alignItems: 'center',
  },
  actionSheetTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
  },
  actionSheetSubtitle: {
    fontSize: 14,
    color: '#666',
    marginTop: 4,
  },
  actionButton: {
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  actionButtonText: {
    fontSize: 16,
    color: '#003366',
    textAlign: 'center',
  },
  destructiveButton: {
    backgroundColor: '#fff',
  },
  destructiveButtonText: {
    fontSize: 16,
    color: '#dc3545',
    textAlign: 'center',
  },
  cancelButton: {
    padding: 16,
    marginTop: 8,
    backgroundColor: '#f5f5f5',
    marginHorizontal: 16,
    borderRadius: 8,
  },
  cancelButtonText: {
    fontSize: 16,
    color: '#666',
    textAlign: 'center',
    fontWeight: '600',
  },
});
