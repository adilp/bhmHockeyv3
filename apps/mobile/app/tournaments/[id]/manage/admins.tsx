import { useState, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  Modal,
  TextInput,
  Alert,
  ActivityIndicator,
  RefreshControl,
  Platform,
  ActionSheetIOS,
  KeyboardAvoidingView,
  TouchableWithoutFeedback,
} from 'react-native';
import { useLocalSearchParams, Stack, useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useTournamentStore } from '../../../../stores/tournamentStore';
import { useAuthStore } from '../../../../stores/authStore';
import { adminService } from '@bhmhockey/api-client';
import { colors, spacing, radius } from '../../../../theme';
import { EmptyState } from '../../../../components';
import type { TournamentAdminDto, TournamentAdminRole, AdminUserSearchResult } from '@bhmhockey/shared';

// Role badge styling
const getRoleBadgeStyle = (role: TournamentAdminRole) => {
  switch (role) {
    case 'Owner':
      return { bg: colors.primary.teal + '20', text: colors.primary.teal };
    case 'Admin':
      return { bg: colors.primary.purple + '20', text: colors.primary.purple };
    case 'Scorekeeper':
      return { bg: colors.primary.blue + '20', text: colors.primary.blue };
  }
};

export default function AdminsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [showAddModal, setShowAddModal] = useState(false);
  const [showTransferModal, setShowTransferModal] = useState(false);

  const { admins, fetchAdmins, updateAdminRole, removeAdmin, transferOwnership, isLoadingAdmins } = useTournamentStore();
  const currentUser = useAuthStore(state => state.user);

  // Check if current user is Owner
  const currentUserAdmin = admins.find(a => a.userId === currentUser?.id);
  const isOwner = currentUserAdmin?.role === 'Owner';

  useFocusEffect(
    useCallback(() => {
      if (id) fetchAdmins(id);
    }, [id])
  );

  const handleRefresh = async () => {
    if (!id) return;
    setIsRefreshing(true);
    try {
      await fetchAdmins(id);
    } finally {
      setIsRefreshing(false);
    }
  };

  const handleAdminPress = (admin: TournamentAdminDto) => {
    // Only owner can edit
    if (!isOwner || !id) return;

    // Owner can't edit themselves
    if (admin.role === 'Owner') {
      Alert.alert('Cannot Edit', 'The tournament owner cannot be edited. Use "Transfer Ownership" to change the owner.');
      return;
    }

    const adminName = `${admin.userFirstName} ${admin.userLastName}`;

    if (Platform.OS === 'ios') {
      ActionSheetIOS.showActionSheetWithOptions(
        {
          options: ['Cancel', 'Change to Admin', 'Change to Scorekeeper', 'Remove Admin'],
          cancelButtonIndex: 0,
          destructiveButtonIndex: 3,
          title: `${adminName} - ${admin.role}`,
        },
        async (buttonIndex) => {
          if (buttonIndex === 0) return; // Cancel

          if (buttonIndex === 1) {
            // Change to Admin
            const success = await updateAdminRole(id, admin.userId, 'Admin');
            if (success) {
              Alert.alert('Success', `${adminName} is now an Admin`);
            }
          } else if (buttonIndex === 2) {
            // Change to Scorekeeper
            const success = await updateAdminRole(id, admin.userId, 'Scorekeeper');
            if (success) {
              Alert.alert('Success', `${adminName} is now a Scorekeeper`);
            }
          } else if (buttonIndex === 3) {
            // Remove Admin
            Alert.alert(
              'Remove Admin',
              `Remove ${adminName} from tournament admins?`,
              [
                { text: 'Cancel', style: 'cancel' },
                {
                  text: 'Remove',
                  style: 'destructive',
                  onPress: async () => {
                    const success = await removeAdmin(id, admin.userId);
                    if (success) {
                      Alert.alert('Done', `${adminName} removed`);
                    }
                  },
                },
              ]
            );
          }
        }
      );
    } else {
      // Android - use Alert.alert with buttons
      Alert.alert(`${adminName} - ${admin.role}`, 'Choose an action:', [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Change to Admin',
          onPress: async () => {
            const success = await updateAdminRole(id, admin.userId, 'Admin');
            if (success) {
              Alert.alert('Success', `${adminName} is now an Admin`);
            }
          },
        },
        {
          text: 'Change to Scorekeeper',
          onPress: async () => {
            const success = await updateAdminRole(id, admin.userId, 'Scorekeeper');
            if (success) {
              Alert.alert('Success', `${adminName} is now a Scorekeeper`);
            }
          },
        },
        {
          text: 'Remove Admin',
          style: 'destructive',
          onPress: async () => {
            const success = await removeAdmin(id, admin.userId);
            if (success) {
              Alert.alert('Done', `${adminName} removed`);
            }
          },
        },
      ]);
    }
  };

  const handleTransferOwnership = () => {
    if (!id) return;

    // Filter to only show Admins (not Scorekeepers or Owner)
    const eligibleAdmins = admins.filter(a => a.role === 'Admin');

    if (eligibleAdmins.length === 0) {
      Alert.alert('No Eligible Admins', 'You must have at least one Admin to transfer ownership to. Add an Admin first.');
      return;
    }

    setShowTransferModal(true);
  };

  const confirmTransferOwnership = async (newOwnerUserId: string) => {
    if (!id) return;

    const newOwner = admins.find(a => a.userId === newOwnerUserId);
    if (!newOwner) return;

    const newOwnerName = `${newOwner.userFirstName} ${newOwner.userLastName}`;

    Alert.alert(
      'Transfer Ownership',
      `This will make ${newOwnerName} the new owner. You will become an Admin. This action cannot be undone.`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Transfer',
          style: 'destructive',
          onPress: async () => {
            const success = await transferOwnership(id, newOwnerUserId);
            if (success) {
              setShowTransferModal(false);
              Alert.alert('Success', `${newOwnerName} is now the tournament owner`);
            }
          },
        },
      ]
    );
  };

  const renderAdmin = ({ item }: { item: TournamentAdminDto }) => {
    const badgeStyle = getRoleBadgeStyle(item.role);
    const canEdit = isOwner && item.role !== 'Owner';

    return (
      <TouchableOpacity
        style={styles.adminRow}
        onPress={() => handleAdminPress(item)}
        disabled={!canEdit}
        activeOpacity={canEdit ? 0.7 : 1}
      >
        {/* Left section: Admin info */}
        <View style={styles.adminLeft}>
          <View style={styles.nameRow}>
            {item.role === 'Owner' && (
              <Ionicons name="ribbon" size={16} color={colors.primary.teal} style={styles.crownIcon} />
            )}
            <Text style={styles.adminName}>
              {item.userFirstName} {item.userLastName}
            </Text>
          </View>
          <Text style={styles.adminEmail}>{item.userEmail}</Text>
        </View>

        {/* Right section: Role badge */}
        <View style={[styles.roleBadge, { backgroundColor: badgeStyle.bg }]}>
          <Text style={[styles.roleBadgeText, { color: badgeStyle.text }]}>
            {item.role}
          </Text>
        </View>
      </TouchableOpacity>
    );
  };

  const ListEmptyComponent = () => (
    <View style={styles.emptyContainer}>
      <EmptyState
        title="No Admins"
        message="This tournament has no administrators yet."
      />
    </View>
  );

  const headerTitle = `Admins (${admins.length})`;

  // Show loading spinner only on initial load (not during refresh)
  if (isLoadingAdmins && admins.length === 0) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Admins',
            headerBackTitle: 'Back',
            headerStyle: { backgroundColor: colors.bg.dark },
            headerTintColor: colors.text.primary,
          }}
        />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
          <Text style={styles.loadingText}>Loading admins...</Text>
        </View>
      </>
    );
  }

  return (
    <View style={styles.container}>
      <Stack.Screen
        options={{
          title: headerTitle,
          headerBackTitle: 'Back',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
          headerRight: isOwner ? () => (
            <TouchableOpacity onPress={handleTransferOwnership} style={styles.headerButton}>
              <Ionicons name="swap-horizontal" size={24} color={colors.primary.teal} />
            </TouchableOpacity>
          ) : undefined,
        }}
      />

      <FlatList
        data={admins}
        keyExtractor={(item) => item.id}
        renderItem={renderAdmin}
        ListEmptyComponent={ListEmptyComponent}
        contentContainerStyle={[
          styles.listContent,
          admins.length === 0 && styles.emptyListContent,
        ]}
        refreshControl={
          <RefreshControl
            refreshing={isRefreshing}
            onRefresh={handleRefresh}
            tintColor={colors.primary.teal}
            colors={[colors.primary.teal]}
          />
        }
        ItemSeparatorComponent={() => <View style={styles.separator} />}
      />

      {/* Floating Action Button (Owner only) */}
      {isOwner && (
        <TouchableOpacity
          style={styles.fab}
          onPress={() => setShowAddModal(true)}
          activeOpacity={0.8}
        >
          <Ionicons name="add" size={28} color={colors.bg.darkest} />
        </TouchableOpacity>
      )}

      {/* Add Admin Modal */}
      {showAddModal && id && (
        <AddAdminModal
          visible={showAddModal}
          tournamentId={id}
          existingAdminUserIds={admins.map(a => a.userId)}
          onClose={() => setShowAddModal(false)}
        />
      )}

      {/* Transfer Ownership Modal */}
      {showTransferModal && (
        <TransferOwnershipModal
          visible={showTransferModal}
          admins={admins.filter(a => a.role === 'Admin')}
          onSelectAdmin={confirmTransferOwnership}
          onClose={() => setShowTransferModal(false)}
        />
      )}
    </View>
  );
}

// ============================================
// Add Admin Modal
// ============================================

interface AddAdminModalProps {
  visible: boolean;
  tournamentId: string;
  existingAdminUserIds: string[];
  onClose: () => void;
}

function AddAdminModal({ visible, tournamentId, existingAdminUserIds, onClose }: AddAdminModalProps) {
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<AdminUserSearchResult[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedRole, setSelectedRole] = useState<'Admin' | 'Scorekeeper'>('Admin');

  const { addAdmin } = useTournamentStore();

  // Debounced search with 300ms delay
  const [searchTimeout, setSearchTimeout] = useState<ReturnType<typeof setTimeout> | null>(null);

  const handleSearchChange = (query: string) => {
    setSearchQuery(query);

    // Clear previous timeout
    if (searchTimeout) {
      clearTimeout(searchTimeout);
    }

    // Reset results if query is too short
    if (query.length < 2) {
      setSearchResults([]);
      setError(null);
      return;
    }

    // Set up new timeout
    const timeout = setTimeout(() => {
      performSearch(query);
    }, 300);

    setSearchTimeout(timeout);
  };

  const performSearch = async (query: string) => {
    setIsLoading(true);
    setError(null);

    try {
      const results = await adminService.searchUsers(query);

      // Filter out users who are already admins
      const filtered = results.filter(
        user => !existingAdminUserIds.includes(user.id)
      );

      setSearchResults(filtered);
    } catch (err: any) {
      const errorMessage = err?.response?.data?.message || err?.message || 'Failed to search users';
      setError(errorMessage);
      setSearchResults([]);
    } finally {
      setIsLoading(false);
    }
  };

  const handleSelectUser = async (user: AdminUserSearchResult) => {
    const success = await addAdmin(tournamentId, user.id, selectedRole);
    if (success) {
      Alert.alert('Success', `${user.firstName} ${user.lastName} added as ${selectedRole}`);
      // Reset modal state
      setSearchQuery('');
      setSearchResults([]);
      setError(null);
      setSelectedRole('Admin');
      onClose();
    }
  };

  const handleClose = () => {
    // Reset state when closing
    setSearchQuery('');
    setSearchResults([]);
    setError(null);
    setSelectedRole('Admin');
    onClose();
  };

  const renderUserItem = ({ item }: { item: AdminUserSearchResult }) => (
    <TouchableOpacity
      style={styles.userItem}
      onPress={() => handleSelectUser(item)}
      activeOpacity={0.7}
    >
      <View style={styles.userInfo}>
        <Text style={styles.userName}>
          {item.firstName} {item.lastName}
        </Text>
        <Text style={styles.userEmail}>{item.email}</Text>
      </View>
    </TouchableOpacity>
  );

  const renderContent = () => {
    if (searchQuery.length < 2) {
      return (
        <View style={styles.emptySearchContainer}>
          <Text style={styles.emptySearchText}>
            Type at least 2 characters to search by email
          </Text>
        </View>
      );
    }

    if (isLoading) {
      return (
        <View style={styles.emptySearchContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
          <Text style={[styles.emptySearchText, { marginTop: spacing.md }]}>
            Searching...
          </Text>
        </View>
      );
    }

    if (error) {
      return (
        <View style={styles.emptySearchContainer}>
          <Text style={styles.errorText}>{error}</Text>
        </View>
      );
    }

    if (searchResults.length === 0) {
      return (
        <View style={styles.emptySearchContainer}>
          <Text style={styles.emptySearchText}>
            No users found
          </Text>
        </View>
      );
    }

    return (
      <FlatList
        data={searchResults}
        renderItem={renderUserItem}
        keyExtractor={(item) => item.id}
        style={styles.resultsList}
        contentContainerStyle={styles.resultsContent}
        keyboardShouldPersistTaps="handled"
      />
    );
  };

  return (
    <Modal
      visible={visible}
      transparent
      animationType="slide"
      onRequestClose={handleClose}
    >
      <TouchableWithoutFeedback onPress={handleClose}>
        <View style={styles.overlay}>
          <TouchableWithoutFeedback>
            <KeyboardAvoidingView
              behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
              style={styles.modalContainer}
            >
              <View style={styles.modal}>
                {/* Header */}
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle}>Add Admin</Text>
                  <TouchableOpacity
                    style={styles.closeButton}
                    onPress={handleClose}
                    hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                  >
                    <Text style={styles.closeButtonText}>Close</Text>
                  </TouchableOpacity>
                </View>

                {/* Role Selector */}
                <View style={styles.roleSelectorContainer}>
                  <Text style={styles.roleSelectorLabel}>Role:</Text>
                  <View style={styles.roleSelectorButtons}>
                    <TouchableOpacity
                      style={[
                        styles.roleButton,
                        selectedRole === 'Admin' && styles.roleButtonActive,
                      ]}
                      onPress={() => setSelectedRole('Admin')}
                    >
                      <Text
                        style={[
                          styles.roleButtonText,
                          selectedRole === 'Admin' && styles.roleButtonTextActive,
                        ]}
                      >
                        Admin
                      </Text>
                    </TouchableOpacity>
                    <TouchableOpacity
                      style={[
                        styles.roleButton,
                        selectedRole === 'Scorekeeper' && styles.roleButtonActive,
                      ]}
                      onPress={() => setSelectedRole('Scorekeeper')}
                    >
                      <Text
                        style={[
                          styles.roleButtonText,
                          selectedRole === 'Scorekeeper' && styles.roleButtonTextActive,
                        ]}
                      >
                        Scorekeeper
                      </Text>
                    </TouchableOpacity>
                  </View>
                </View>

                {/* Search Input */}
                <View style={styles.searchContainer}>
                  <TextInput
                    style={styles.searchInput}
                    placeholder="Search by email..."
                    placeholderTextColor={colors.text.muted}
                    value={searchQuery}
                    onChangeText={handleSearchChange}
                    autoFocus
                    autoCapitalize="none"
                    autoCorrect={false}
                    returnKeyType="search"
                  />
                </View>

                {/* Results */}
                <View style={styles.resultsContainer}>
                  {renderContent()}
                </View>
              </View>
            </KeyboardAvoidingView>
          </TouchableWithoutFeedback>
        </View>
      </TouchableWithoutFeedback>
    </Modal>
  );
}

// ============================================
// Transfer Ownership Modal
// ============================================

interface TransferOwnershipModalProps {
  visible: boolean;
  admins: TournamentAdminDto[];
  onSelectAdmin: (userId: string) => void;
  onClose: () => void;
}

function TransferOwnershipModal({ visible, admins, onSelectAdmin, onClose }: TransferOwnershipModalProps) {
  const renderAdminItem = ({ item }: { item: TournamentAdminDto }) => (
    <TouchableOpacity
      style={styles.transferAdminItem}
      onPress={() => onSelectAdmin(item.userId)}
      activeOpacity={0.7}
    >
      <View style={styles.transferAdminInfo}>
        <Text style={styles.transferAdminName}>
          {item.userFirstName} {item.userLastName}
        </Text>
        <Text style={styles.transferAdminEmail}>{item.userEmail}</Text>
      </View>
      <Ionicons name="chevron-forward" size={20} color={colors.text.muted} />
    </TouchableOpacity>
  );

  return (
    <Modal
      visible={visible}
      transparent
      animationType="slide"
      onRequestClose={onClose}
    >
      <TouchableWithoutFeedback onPress={onClose}>
        <View style={styles.overlay}>
          <TouchableWithoutFeedback>
            <View style={styles.transferModal}>
              {/* Header */}
              <View style={styles.modalHeader}>
                <Text style={styles.modalTitle}>Transfer Ownership</Text>
                <TouchableOpacity
                  style={styles.closeButton}
                  onPress={onClose}
                  hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                >
                  <Text style={styles.closeButtonText}>Cancel</Text>
                </TouchableOpacity>
              </View>

              <Text style={styles.transferDescription}>
                Select an Admin to become the new owner. You will become an Admin.
              </Text>

              {/* Admin List */}
              <FlatList
                data={admins}
                renderItem={renderAdminItem}
                keyExtractor={(item) => item.id}
                style={styles.transferList}
                contentContainerStyle={styles.transferListContent}
                ItemSeparatorComponent={() => <View style={styles.transferSeparator} />}
              />
            </View>
          </TouchableWithoutFeedback>
        </View>
      </TouchableWithoutFeedback>
    </Modal>
  );
}

// ============================================
// Styles
// ============================================

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
  loadingText: {
    marginTop: spacing.sm,
    fontSize: 16,
    color: colors.text.muted,
  },
  listContent: {
    padding: spacing.md,
  },
  emptyListContent: {
    flex: 1,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingTop: spacing.xl,
  },
  headerButton: {
    marginRight: spacing.sm,
  },

  // Admin row
  adminRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  adminLeft: {
    flex: 1,
    marginRight: spacing.sm,
  },
  nameRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: spacing.xs,
  },
  crownIcon: {
    marginRight: spacing.xs,
  },
  adminName: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.text.primary,
  },
  adminEmail: {
    fontSize: 12,
    color: colors.text.muted,
  },
  roleBadge: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: radius.sm,
  },
  roleBadgeText: {
    fontSize: 12,
    fontWeight: '600',
  },
  separator: {
    height: spacing.sm,
  },

  // Floating Action Button
  fab: {
    position: 'absolute',
    bottom: spacing.xl,
    right: spacing.xl,
    width: 56,
    height: 56,
    borderRadius: radius.round,
    backgroundColor: colors.primary.teal,
    justifyContent: 'center',
    alignItems: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
    elevation: 8,
  },

  // Modal Styles
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.7)',
    justifyContent: 'flex-end',
  },
  modalContainer: {
    flex: 1,
    justifyContent: 'flex-end',
  },
  modal: {
    backgroundColor: colors.bg.dark,
    borderTopLeftRadius: radius.xl,
    borderTopRightRadius: radius.xl,
    maxHeight: '80%',
    paddingBottom: spacing.xl + 20, // Extra padding for home indicator
  },
  modalHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  modalTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.text.primary,
  },
  closeButton: {
    padding: spacing.sm,
  },
  closeButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.muted,
  },

  // Role Selector
  roleSelectorContainer: {
    padding: spacing.lg,
    paddingBottom: spacing.md,
  },
  roleSelectorLabel: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.secondary,
    marginBottom: spacing.sm,
  },
  roleSelectorButtons: {
    flexDirection: 'row',
    gap: spacing.sm,
  },
  roleButton: {
    flex: 1,
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    borderRadius: radius.md,
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.default,
    alignItems: 'center',
  },
  roleButtonActive: {
    backgroundColor: colors.primary.purple + '20',
    borderColor: colors.primary.purple,
  },
  roleButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.secondary,
  },
  roleButtonTextActive: {
    color: colors.primary.purple,
  },

  // Search
  searchContainer: {
    paddingHorizontal: spacing.lg,
    paddingBottom: spacing.md,
  },
  searchInput: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    fontSize: 16,
    color: colors.text.primary,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  resultsContainer: {
    flex: 1,
    minHeight: 200,
  },
  resultsList: {
    flex: 1,
  },
  resultsContent: {
    paddingHorizontal: spacing.lg,
    paddingBottom: spacing.lg,
  },
  userItem: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.sm,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  userInfo: {
    flex: 1,
  },
  userName: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: 2,
  },
  userEmail: {
    fontSize: 14,
    color: colors.text.muted,
  },
  emptySearchContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: spacing.xl,
  },
  emptySearchText: {
    fontSize: 16,
    color: colors.text.muted,
    textAlign: 'center',
  },
  errorText: {
    fontSize: 16,
    color: colors.status.error,
    textAlign: 'center',
  },

  // Transfer Ownership Modal
  transferModal: {
    backgroundColor: colors.bg.dark,
    borderTopLeftRadius: radius.xl,
    borderTopRightRadius: radius.xl,
    maxHeight: '60%',
    paddingBottom: spacing.xl + 20,
  },
  transferDescription: {
    fontSize: 14,
    color: colors.text.secondary,
    paddingHorizontal: spacing.lg,
    paddingBottom: spacing.md,
    textAlign: 'center',
  },
  transferList: {
    flex: 1,
  },
  transferListContent: {
    paddingHorizontal: spacing.lg,
    paddingBottom: spacing.lg,
  },
  transferAdminItem: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: colors.bg.elevated,
    padding: spacing.md,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  transferAdminInfo: {
    flex: 1,
    marginRight: spacing.sm,
  },
  transferAdminName: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: 2,
  },
  transferAdminEmail: {
    fontSize: 12,
    color: colors.text.muted,
  },
  transferSeparator: {
    height: spacing.sm,
  },
});
