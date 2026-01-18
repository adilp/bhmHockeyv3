import React, { useState, useEffect, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  TextInput,
  FlatList,
  ActivityIndicator,
  Alert,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { useRouter, useLocalSearchParams, Stack } from 'expo-router';
import type { UserSearchResultDto } from '@bhmhockey/shared';
import { useTournamentTeamStore } from '../../../../../stores/tournamentTeamStore';
import { colors, spacing, radius } from '../../../../../theme';

export default function AddPlayerScreen() {
  const router = useRouter();
  const { id: tournamentId, teamId } = useLocalSearchParams<{ id: string; teamId: string }>();

  const teamMembers = useTournamentTeamStore(state => state.teamMembers);
  const addPlayer = useTournamentTeamStore(state => state.addPlayer);
  const isProcessing = useTournamentTeamStore(state => state.isProcessing);

  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<UserSearchResultDto[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [addingUserId, setAddingUserId] = useState<string | null>(null);

  // Get existing member IDs to filter them out from search results
  const existingMemberIds = teamMembers.map(member => member.userId);

  // Debounced search with 300ms delay
  useEffect(() => {
    // Reset results if query is too short
    if (searchQuery.length < 2) {
      setSearchResults([]);
      setError(null);
      return;
    }

    // Set up debounce timer
    const timer = setTimeout(() => {
      performSearch(searchQuery);
    }, 300);

    // Cleanup timer on query change
    return () => clearTimeout(timer);
  }, [searchQuery, tournamentId, teamId]);

  const performSearch = async (query: string) => {
    if (!tournamentId || !teamId) return;

    setIsSearching(true);
    setError(null);

    try {
      const results = await useTournamentTeamStore.getState().searchUsers(
        tournamentId,
        teamId,
        query
      );

      // Filter out users who are already team members
      const filtered = results.filter(
        user => !existingMemberIds.includes(user.id)
      );

      setSearchResults(filtered);
    } catch (err: any) {
      const errorMessage = err?.response?.data?.message || err?.message || 'Failed to search users';
      setError(errorMessage);
      setSearchResults([]);
    } finally {
      setIsSearching(false);
    }
  };

  const handleAddPlayer = async (user: UserSearchResultDto) => {
    if (!tournamentId || !teamId || addingUserId || isProcessing) return;

    setAddingUserId(user.id);

    try {
      const success = await addPlayer(tournamentId, teamId, user.id);

      if (success) {
        Alert.alert(
          'Player Added',
          `${user.firstName} ${user.lastName} has been added to the team.`,
          [
            {
              text: 'Add Another',
              onPress: () => {
                // Remove the added user from search results
                setSearchResults(prev => prev.filter(u => u.id !== user.id));
                setAddingUserId(null);
              }
            },
            {
              text: 'Done',
              onPress: () => router.back()
            }
          ]
        );
      } else {
        Alert.alert('Error', 'Failed to add player. Please try again.');
        setAddingUserId(null);
      }
    } catch (err) {
      Alert.alert('Error', 'Failed to add player. Please try again.');
      setAddingUserId(null);
    }
  };

  const renderUserItem = ({ item }: { item: UserSearchResultDto }) => {
    const isAdding = addingUserId === item.id;

    return (
      <View style={styles.userItem}>
        <View style={styles.userInfo}>
          <Text style={styles.userName} allowFontScaling={false}>
            {item.firstName} {item.lastName}
          </Text>
          <Text style={styles.userEmail} allowFontScaling={false}>
            {item.email}
          </Text>
        </View>
        <TouchableOpacity
          style={[
            styles.addButton,
            (isAdding || isProcessing) && styles.addButtonDisabled
          ]}
          onPress={() => handleAddPlayer(item)}
          disabled={isAdding || isProcessing}
          activeOpacity={0.7}
        >
          {isAdding ? (
            <ActivityIndicator size="small" color={colors.text.primary} />
          ) : (
            <Text style={styles.addButtonText} allowFontScaling={false}>
              Add
            </Text>
          )}
        </TouchableOpacity>
      </View>
    );
  };

  const renderContent = () => {
    if (searchQuery.length < 2) {
      return (
        <View style={styles.emptyContainer}>
          <Text style={styles.emptyText} allowFontScaling={false}>
            Type at least 2 characters to search
          </Text>
        </View>
      );
    }

    if (isSearching) {
      return (
        <View style={styles.emptyContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
          <Text style={[styles.emptyText, { marginTop: spacing.md }]} allowFontScaling={false}>
            Searching...
          </Text>
        </View>
      );
    }

    if (error) {
      return (
        <View style={styles.emptyContainer}>
          <Text style={styles.errorText} allowFontScaling={false}>
            {error}
          </Text>
        </View>
      );
    }

    if (searchResults.length === 0) {
      return (
        <View style={styles.emptyContainer}>
          <Text style={styles.emptyText} allowFontScaling={false}>
            No users found
          </Text>
          <Text style={[styles.emptyText, { marginTop: spacing.sm, fontSize: 13 }]} allowFontScaling={false}>
            {existingMemberIds.length > 0
              ? 'Users already on the team are filtered out'
              : 'Try a different search term'
            }
          </Text>
        </View>
      );
    }

    return (
      <FlatList
        data={searchResults}
        renderItem={renderUserItem}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.resultsContent}
        keyboardShouldPersistTaps="handled"
      />
    );
  };

  return (
    <View style={styles.container}>
      <Stack.Screen
        options={{
          title: 'Add Player',
          headerStyle: {
            backgroundColor: colors.bg.dark,
          },
          headerTintColor: colors.text.primary,
        }}
      />

      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
        style={styles.keyboardView}
      >
        {/* Search Input */}
        <View style={styles.searchContainer}>
          <TextInput
            style={styles.searchInput}
            placeholder="Search by name or email..."
            placeholderTextColor={colors.text.muted}
            value={searchQuery}
            onChangeText={setSearchQuery}
            autoFocus
            autoCapitalize="none"
            autoCorrect={false}
            returnKeyType="search"
          />
        </View>

        {/* Results */}
        <View style={styles.resultsContainer}>
          <View style={styles.sectionHeader}>
            <Text style={styles.sectionTitle} allowFontScaling={false}>
              SEARCH RESULTS
            </Text>
          </View>
          {renderContent()}
        </View>
      </KeyboardAvoidingView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
  keyboardView: {
    flex: 1,
  },
  searchContainer: {
    padding: spacing.lg,
    paddingBottom: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
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
  sectionHeader: {
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  resultsContainer: {
    flex: 1,
  },
  resultsContent: {
    paddingHorizontal: spacing.lg,
    paddingBottom: spacing.lg,
  },
  userItem: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.sm,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  userInfo: {
    flex: 1,
    marginRight: spacing.md,
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
  addButton: {
    backgroundColor: colors.primary.teal,
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.lg,
    borderRadius: radius.md,
    minWidth: 70,
    alignItems: 'center',
    justifyContent: 'center',
  },
  addButtonDisabled: {
    backgroundColor: colors.bg.hover,
    opacity: 0.6,
  },
  addButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.primary,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: spacing.xl,
  },
  emptyText: {
    fontSize: 16,
    color: colors.text.muted,
    textAlign: 'center',
  },
  errorText: {
    fontSize: 16,
    color: colors.status.error,
    textAlign: 'center',
  },
});
