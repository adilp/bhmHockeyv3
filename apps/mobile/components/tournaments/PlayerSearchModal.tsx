import React, { useState, useEffect, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  TouchableWithoutFeedback,
  TextInput,
  FlatList,
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import type { UserSearchResultDto } from '@bhmhockey/shared';
import { apiClient } from '@bhmhockey/api-client';
import { colors, spacing, radius } from '../../theme';

interface PlayerSearchModalProps {
  visible: boolean;
  onClose: () => void;
  onSelectUser: (user: UserSearchResultDto) => void;
  tournamentId: string;
  teamId: string;
  existingMemberIds: string[]; // To filter out already added users
}

export function PlayerSearchModal({
  visible,
  onClose,
  onSelectUser,
  tournamentId,
  teamId,
  existingMemberIds,
}: PlayerSearchModalProps) {
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<UserSearchResultDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

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
    setIsLoading(true);
    setError(null);

    try {
      const response = await apiClient.instance.post<UserSearchResultDto[]>(
        `/tournaments/${tournamentId}/teams/${teamId}/search-users`,
        null,
        { params: { query } }
      );

      // Filter out users who are already team members
      const filtered = response.data.filter(
        user => !existingMemberIds.includes(user.id)
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

  const handleSelectUser = (user: UserSearchResultDto) => {
    onSelectUser(user);
    // Reset modal state
    setSearchQuery('');
    setSearchResults([]);
    setError(null);
  };

  const handleClose = () => {
    // Reset state when closing
    setSearchQuery('');
    setSearchResults([]);
    setError(null);
    onClose();
  };

  const renderUserItem = ({ item }: { item: UserSearchResultDto }) => (
    <TouchableOpacity
      style={styles.userItem}
      onPress={() => handleSelectUser(item)}
      activeOpacity={0.7}
    >
      <View style={styles.userInfo}>
        <Text style={styles.userName} allowFontScaling={false}>
          {item.firstName} {item.lastName}
        </Text>
        <Text style={styles.userEmail} allowFontScaling={false}>
          {item.email}
        </Text>
      </View>
    </TouchableOpacity>
  );

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

    if (isLoading) {
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
                <View style={styles.header}>
                  <Text style={styles.title} allowFontScaling={false}>
                    Add Player
                  </Text>
                  <TouchableOpacity
                    style={styles.closeButton}
                    onPress={handleClose}
                    hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                  >
                    <Text style={styles.closeButtonText} allowFontScaling={false}>
                      Close
                    </Text>
                  </TouchableOpacity>
                </View>

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

const styles = StyleSheet.create({
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
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  title: {
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
  searchContainer: {
    padding: spacing.lg,
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
