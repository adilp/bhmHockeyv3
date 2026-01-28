import { useState, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TextInput,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
  Clipboard,
} from 'react-native';
import { useRouter } from 'expo-router';
import { useFocusEffect } from '@react-navigation/native';
import { adminService } from '@bhmhockey/api-client';
import { useAuthStore } from '../../stores/authStore';
import type { AdminUserSearchResult, AdminPasswordResetResponse, AdminStatsResponse, UserRole } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../../theme';

const ROLE_OPTIONS: UserRole[] = ['Player', 'Organizer', 'Admin'];

const getRoleBadgeStyle = (role: UserRole) => {
  switch (role) {
    case 'Admin':
      return { backgroundColor: colors.primary.purple };
    case 'Organizer':
      return { backgroundColor: colors.primary.teal };
    default:
      return { backgroundColor: colors.bg.elevated };
  }
};

export default function AdminScreen() {
  const router = useRouter();
  const { user } = useAuthStore();

  const [searchEmail, setSearchEmail] = useState('');
  const [searchResults, setSearchResults] = useState<AdminUserSearchResult[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [isResetting, setIsResetting] = useState<string | null>(null);
  const [isUpdatingRole, setIsUpdatingRole] = useState<string | null>(null);
  const [lastResetResult, setLastResetResult] = useState<AdminPasswordResetResponse | null>(null);
  const [stats, setStats] = useState<AdminStatsResponse | null>(null);

  // Redirect if not admin, and fetch stats
  useFocusEffect(
    useCallback(() => {
      if (user?.role !== 'Admin') {
        router.replace('/(tabs)/profile');
        return;
      }

      // Fetch admin stats
      adminService.getStats()
        .then(setStats)
        .catch(console.error);
    }, [user, router])
  );

  const handleSearch = async () => {
    if (searchEmail.length < 2) {
      Alert.alert('Error', 'Please enter at least 2 characters to search');
      return;
    }

    try {
      setIsSearching(true);
      setLastResetResult(null);
      console.log('Searching for:', searchEmail);
      const results = await adminService.searchUsers(searchEmail);
      console.log('Results:', results);
      setSearchResults(results);
      if (results.length === 0) {
        Alert.alert('No Results', 'No users found matching that search');
      }
    } catch (error: any) {
      console.error('Search failed:', error?.message);
      const message = error?.message || error?.response?.data?.message || 'Failed to search users';
      Alert.alert('Error', `${message} (Status: ${error?.statusCode || 'unknown'})`);
    } finally {
      setIsSearching(false);
    }
  };

  const handleResetPassword = async (userId: string, email: string) => {
    Alert.alert(
      'Reset Password',
      `Are you sure you want to reset the password for ${email}?`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Reset',
          style: 'destructive',
          onPress: async () => {
            try {
              setIsResetting(userId);
              const result = await adminService.resetUserPassword(userId);
              setLastResetResult(result);
              Alert.alert(
                'Password Reset',
                `Temporary password: ${result.temporaryPassword}\n\nTap OK to copy to clipboard.`,
                [
                  {
                    text: 'OK',
                    onPress: () => {
                      Clipboard.setString(result.temporaryPassword);
                    },
                  },
                ]
              );
            } catch (error: any) {
              console.error('Password reset failed:', error);
              const message = error?.response?.data?.message || 'Failed to reset password';
              Alert.alert('Error', message);
            } finally {
              setIsResetting(null);
            }
          },
        },
      ]
    );
  };

  const handleChangeRole = async (userId: string, userName: string, currentRole: UserRole, newRole: UserRole) => {
    if (currentRole === newRole) return;

    Alert.alert(
      'Change Role',
      `Change ${userName} from ${currentRole} to ${newRole}?`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Confirm',
          onPress: async () => {
            try {
              setIsUpdatingRole(userId);
              const result = await adminService.updateUserRole(userId, newRole);

              // Update local state
              setSearchResults(prev =>
                prev.map(u => u.id === userId ? { ...u, role: newRole } : u)
              );

              Alert.alert('Success', result.message);
            } catch (error: any) {
              console.error('Role update failed:', error);
              const message = error?.message || error?.response?.data?.message || 'Failed to update role';
              Alert.alert('Error', message);
            } finally {
              setIsUpdatingRole(null);
            }
          },
        },
      ]
    );
  };

  const copyToClipboard = (text: string) => {
    Clipboard.setString(text);
    Alert.alert('Copied', 'Password copied to clipboard');
  };

  if (user?.role !== 'Admin') {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
      </View>
    );
  }

  return (
    <ScrollView style={styles.container}>
      <View style={styles.content}>
        {/* Stats Section */}
        {stats && (
          <View style={styles.statsRow}>
            <View style={styles.statCard}>
              <Text style={styles.statValue}>{stats.totalUsers}</Text>
              <Text style={styles.statLabel}>Total Users</Text>
            </View>
            <View style={styles.statCard}>
              <Text style={styles.statValue}>{stats.activeUsers}</Text>
              <Text style={styles.statLabel}>Active Users</Text>
            </View>
          </View>
        )}

        {/* Search Section */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Search User</Text>
          <View style={styles.searchRow}>
            <TextInput
              style={styles.searchInput}
              placeholder="Search by name or email..."
              placeholderTextColor={colors.text.muted}
              value={searchEmail}
              onChangeText={setSearchEmail}
              autoCapitalize="none"
              autoCorrect={false}
              onSubmitEditing={handleSearch}
            />
            <TouchableOpacity
              style={[styles.searchButton, isSearching && styles.buttonDisabled]}
              onPress={handleSearch}
              disabled={isSearching}
            >
              {isSearching ? (
                <ActivityIndicator size="small" color={colors.bg.darkest} />
              ) : (
                <Text style={styles.searchButtonText}>Search</Text>
              )}
            </TouchableOpacity>
          </View>
        </View>

        {/* Last Reset Result */}
        {lastResetResult && (
          <View style={styles.resultCard}>
            <Text style={styles.resultTitle}>Last Reset</Text>
            <Text style={styles.resultEmail}>{lastResetResult.email}</Text>
            <TouchableOpacity
              style={styles.passwordBox}
              onPress={() => copyToClipboard(lastResetResult.temporaryPassword)}
            >
              <Text style={styles.passwordLabel}>Temporary Password (tap to copy):</Text>
              <Text style={styles.passwordValue}>{lastResetResult.temporaryPassword}</Text>
            </TouchableOpacity>
          </View>
        )}

        {/* Search Results */}
        {searchResults.length > 0 && (
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Results ({searchResults.length})</Text>
            {searchResults.map((result) => (
              <View key={result.id} style={styles.userCard}>
                <View style={styles.userHeader}>
                  <View style={styles.userInfo}>
                    <View style={styles.nameRow}>
                      <Text style={styles.userName}>
                        {result.firstName} {result.lastName}
                      </Text>
                      <View style={[styles.roleBadge, getRoleBadgeStyle(result.role)]}>
                        <Text style={styles.roleBadgeText}>{result.role}</Text>
                      </View>
                    </View>
                    <Text style={styles.userEmail}>{result.email}</Text>
                    {!result.isActive && (
                      <Text style={styles.inactiveTag}>Inactive</Text>
                    )}
                  </View>
                  <TouchableOpacity
                    style={[
                      styles.resetButton,
                      (!result.isActive || isResetting === result.id) && styles.buttonDisabled,
                    ]}
                    onPress={() => handleResetPassword(result.id, result.email)}
                    disabled={!result.isActive || isResetting === result.id}
                  >
                    {isResetting === result.id ? (
                      <ActivityIndicator size="small" color={colors.text.primary} />
                    ) : (
                      <Text style={styles.resetButtonText}>Reset PW</Text>
                    )}
                  </TouchableOpacity>
                </View>

                {/* Role Selector */}
                {result.isActive && (
                  <View style={styles.roleSection}>
                    <Text style={styles.roleLabel}>Change Role:</Text>
                    <View style={styles.roleButtons}>
                      {ROLE_OPTIONS.map((role) => (
                        <TouchableOpacity
                          key={role}
                          style={[
                            styles.roleButton,
                            result.role === role && styles.roleButtonActive,
                            isUpdatingRole === result.id && styles.buttonDisabled,
                          ]}
                          onPress={() => handleChangeRole(
                            result.id,
                            `${result.firstName} ${result.lastName}`,
                            result.role,
                            role
                          )}
                          disabled={result.role === role || isUpdatingRole === result.id}
                        >
                          {isUpdatingRole === result.id ? (
                            <ActivityIndicator size="small" color={colors.text.primary} />
                          ) : (
                            <Text style={[
                              styles.roleButtonText,
                              result.role === role && styles.roleButtonTextActive,
                            ]}>
                              {role}
                            </Text>
                          )}
                        </TouchableOpacity>
                      ))}
                    </View>
                  </View>
                )}
              </View>
            ))}
          </View>
        )}

        {/* Back Button */}
        <TouchableOpacity
          style={styles.backButton}
          onPress={() => router.back()}
        >
          <Text style={styles.backButtonText}>Back to Profile</Text>
        </TouchableOpacity>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.darkest,
  },
  content: {
    padding: spacing.lg,
  },
  statsRow: {
    flexDirection: 'row',
    gap: spacing.md,
    marginBottom: spacing.lg,
  },
  statCard: {
    flex: 1,
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.lg,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  statValue: {
    fontSize: 32,
    fontWeight: 'bold',
    color: colors.primary.teal,
  },
  statLabel: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: spacing.xs,
  },
  section: {
    marginBottom: spacing.lg,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: spacing.md,
  },
  searchRow: {
    flexDirection: 'row',
    gap: spacing.sm,
  },
  searchInput: {
    flex: 1,
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    fontSize: 16,
    color: colors.text.primary,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  searchButton: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    paddingHorizontal: spacing.lg,
    justifyContent: 'center',
    alignItems: 'center',
  },
  searchButtonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
  },
  buttonDisabled: {
    opacity: 0.6,
  },
  resultCard: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.lg,
    marginBottom: spacing.lg,
    borderWidth: 1,
    borderColor: colors.primary.green,
  },
  resultTitle: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.primary.green,
    marginBottom: spacing.xs,
  },
  resultEmail: {
    fontSize: 16,
    color: colors.text.primary,
    marginBottom: spacing.md,
  },
  passwordBox: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
  },
  passwordLabel: {
    fontSize: 12,
    color: colors.text.muted,
    marginBottom: spacing.xs,
  },
  passwordValue: {
    fontSize: 20,
    fontWeight: 'bold',
    color: colors.primary.teal,
    fontFamily: 'monospace',
  },
  userCard: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    marginBottom: spacing.sm,
  },
  userHeader: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    justifyContent: 'space-between',
  },
  userInfo: {
    flex: 1,
  },
  nameRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    flexWrap: 'wrap',
  },
  userName: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
  roleBadge: {
    paddingHorizontal: spacing.sm,
    paddingVertical: 2,
    borderRadius: radius.sm,
  },
  roleBadgeText: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.primary,
  },
  userEmail: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: 2,
  },
  inactiveTag: {
    fontSize: 12,
    color: colors.status.error,
    marginTop: spacing.xs,
  },
  resetButton: {
    backgroundColor: colors.status.warning,
    borderRadius: radius.md,
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
  },
  resetButtonText: {
    color: colors.bg.darkest,
    fontSize: 14,
    fontWeight: '600',
  },
  roleSection: {
    marginTop: spacing.md,
    paddingTop: spacing.md,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },
  roleLabel: {
    fontSize: 12,
    color: colors.text.muted,
    marginBottom: spacing.sm,
  },
  roleButtons: {
    flexDirection: 'row',
    gap: spacing.sm,
  },
  roleButton: {
    flex: 1,
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    paddingVertical: spacing.sm,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  roleButtonActive: {
    backgroundColor: colors.primary.teal,
    borderColor: colors.primary.teal,
  },
  roleButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.secondary,
  },
  roleButtonTextActive: {
    color: colors.bg.darkest,
  },
  backButton: {
    backgroundColor: 'transparent',
    borderWidth: 1,
    borderColor: colors.border.default,
    borderRadius: radius.lg,
    padding: spacing.md,
    alignItems: 'center',
    marginTop: spacing.lg,
  },
  backButtonText: {
    color: colors.text.secondary,
    fontSize: 16,
  },
});
