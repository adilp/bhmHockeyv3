import React, { useState, useEffect, useCallback, useRef } from 'react';
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
  Alert,
  KeyboardAvoidingView,
  Keyboard,
  Platform,
  ScrollView,
} from 'react-native';
import type { UserSearchResultDto, SkillLevel } from '@bhmhockey/shared';
import { useEventStore } from '../../stores/eventStore';
import { colors, spacing, radius } from '../../theme';

type TabType = 'search' | 'create';

interface AddPlayerModalProps {
  visible: boolean;
  eventId: string;
  onClose: () => void;
  onPlayerAdded: () => void;
}

export function AddPlayerModal({
  visible,
  eventId,
  onClose,
  onPlayerAdded,
}: AddPlayerModalProps) {
  // Tab state
  const [activeTab, setActiveTab] = useState<TabType>('search');

  // Search tab state
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<UserSearchResultDto[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [selectedUser, setSelectedUser] = useState<UserSearchResultDto | null>(null);
  const [selectedPosition, setSelectedPosition] = useState<string | null>(null);
  const [isAdding, setIsAdding] = useState(false);
  const searchTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  // Create Guest tab state
  const [guestFirstName, setGuestFirstName] = useState('');
  const [guestLastName, setGuestLastName] = useState('');
  const [guestPosition, setGuestPosition] = useState<'Goalie' | 'Skater' | null>(null);
  const [guestSkillLevel, setGuestSkillLevel] = useState<SkillLevel | null>(null);
  const [isCreatingGuest, setIsCreatingGuest] = useState(false);

  const { searchUsersForEvent, addUserToEvent, createGhostPlayer } = useEventStore();

  // Reset state when modal opens/closes
  useEffect(() => {
    if (!visible) {
      // Reset tab
      setActiveTab('search');
      // Reset search tab state
      setQuery('');
      setResults([]);
      setSelectedUser(null);
      setSelectedPosition(null);
      setIsSearching(false);
      setIsAdding(false);
      // Reset create guest tab state
      setGuestFirstName('');
      setGuestLastName('');
      setGuestPosition(null);
      setGuestSkillLevel(null);
      setIsCreatingGuest(false);
    }
  }, [visible]);

  // Debounced search
  useEffect(() => {
    if (searchTimeoutRef.current) {
      clearTimeout(searchTimeoutRef.current);
    }

    if (query.length < 2) {
      setResults([]);
      setIsSearching(false);
      return;
    }

    setIsSearching(true);
    searchTimeoutRef.current = setTimeout(async () => {
      const searchResults = await searchUsersForEvent(eventId, query);
      setResults(searchResults);
      setIsSearching(false);
    }, 300);

    return () => {
      if (searchTimeoutRef.current) {
        clearTimeout(searchTimeoutRef.current);
      }
    };
  }, [query, eventId, searchUsersForEvent]);

  const handleSelectUser = useCallback((user: UserSearchResultDto) => {
    setSelectedUser(user);
    // Always require position selection - organizers can add as any position
    setSelectedPosition(null);
    Keyboard.dismiss();
  }, []);

  const handleConfirmAdd = async () => {
    if (!selectedUser) return;

    // Position is always required
    if (!selectedPosition) {
      Alert.alert('Select Position', 'Please select a position for this player.');
      return;
    }

    setIsAdding(true);
    const success = await addUserToEvent(eventId, selectedUser.id, selectedPosition);
    setIsAdding(false);

    if (success) {
      onPlayerAdded();
      onClose();
    } else {
      // Error is set in store and will show via error handling in parent
    }
  };

  const handleCreateGuest = async () => {
    if (!guestFirstName.trim()) {
      Alert.alert('Required', 'Please enter a first name.');
      return;
    }
    if (!guestLastName.trim()) {
      Alert.alert('Required', 'Please enter a last name.');
      return;
    }
    if (!guestPosition) {
      Alert.alert('Required', 'Please select a position.');
      return;
    }

    setIsCreatingGuest(true);
    const success = await createGhostPlayer(
      eventId,
      guestFirstName.trim(),
      guestLastName.trim(),
      guestPosition,
      guestSkillLevel || undefined
    );
    setIsCreatingGuest(false);

    if (success) {
      onPlayerAdded();
      onClose();
    }
  };

  const renderUserItem = ({ item }: { item: UserSearchResultDto }) => {
    const positions = item.positions ? Object.keys(item.positions) : [];
    const positionText = positions.length > 0
      ? positions.map(p => p === 'goalie' ? 'G' : 'S').join('/')
      : 'No position';

    const isSelected = selectedUser?.id === item.id;

    return (
      <TouchableOpacity
        style={[styles.userItem, isSelected && styles.userItemSelected]}
        onPress={() => handleSelectUser(item)}
      >
        <View style={styles.userInfo}>
          <Text style={styles.userName} allowFontScaling={false}>
            {item.firstName} {item.lastName}
          </Text>
          <Text style={styles.userEmail} allowFontScaling={false}>
            {item.email}
          </Text>
        </View>
        <View style={styles.positionBadge}>
          <Text style={styles.positionText} allowFontScaling={false}>
            {positionText}
          </Text>
        </View>
      </TouchableOpacity>
    );
  };

  return (
    <Modal
      visible={visible}
      transparent
      animationType="slide"
      onRequestClose={onClose}
    >
      <TouchableWithoutFeedback onPress={onClose}>
        <View style={styles.overlay}>
          <KeyboardAvoidingView
            behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
            style={styles.keyboardView}
          >
            <TouchableWithoutFeedback onPress={Keyboard.dismiss}>
              <View style={styles.modal}>
                {/* Header */}
                <View style={styles.header}>
                  <Text style={styles.title} allowFontScaling={false}>Add Player</Text>
                  <TouchableOpacity onPress={onClose} style={styles.closeButton}>
                    <Text style={styles.closeButtonText} allowFontScaling={false}>Cancel</Text>
                  </TouchableOpacity>
                </View>

                {/* Tab Toggle */}
                <View style={styles.tabContainer}>
                  <TouchableOpacity
                    style={[styles.tab, activeTab === 'search' && styles.tabActive]}
                    onPress={() => setActiveTab('search')}
                  >
                    <Text
                      style={[styles.tabText, activeTab === 'search' && styles.tabTextActive]}
                      allowFontScaling={false}
                    >
                      Search
                    </Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={[styles.tab, activeTab === 'create' && styles.tabActive]}
                    onPress={() => setActiveTab('create')}
                  >
                    <Text
                      style={[styles.tabText, activeTab === 'create' && styles.tabTextActive]}
                      allowFontScaling={false}
                    >
                      Create Guest
                    </Text>
                  </TouchableOpacity>
                </View>

                {activeTab === 'search' ? (
                  <>
                    {/* Search Input */}
                    <View style={styles.searchContainer}>
                      <TextInput
                        style={styles.searchInput}
                        placeholder="Search by name..."
                        placeholderTextColor={colors.text.muted}
                        value={query}
                        onChangeText={setQuery}
                        autoCapitalize="none"
                        autoCorrect={false}
                        autoFocus
                        allowFontScaling={false}
                      />
                      {isSearching && (
                        <ActivityIndicator
                          size="small"
                          color={colors.primary.teal}
                          style={styles.searchSpinner}
                        />
                      )}
                    </View>

                    {/* Results or Selected User */}
                    {!selectedUser ? (
                      <>
                        {/* Search Results */}
                        {results.length > 0 ? (
                          <FlatList
                            data={results}
                            keyExtractor={(item) => item.id}
                            renderItem={renderUserItem}
                            style={styles.resultsList}
                            contentContainerStyle={styles.resultsContent}
                            keyboardShouldPersistTaps="handled"
                          />
                        ) : query.length >= 2 && !isSearching ? (
                          <View style={styles.emptyState}>
                            <Text style={styles.emptyText} allowFontScaling={false}>
                              No users found
                            </Text>
                          </View>
                        ) : (
                          <View style={styles.emptyState}>
                            <Text style={styles.emptyText} allowFontScaling={false}>
                              Type at least 2 characters to search
                            </Text>
                          </View>
                        )}
                      </>
                    ) : (
                      <>
                        {/* Selected User Display */}
                        <View style={styles.selectedSection}>
                          <Text style={styles.sectionLabel} allowFontScaling={false}>
                            Selected Player
                          </Text>
                          <View style={styles.selectedUserCard}>
                            <Text style={styles.selectedUserName} allowFontScaling={false}>
                              {selectedUser.firstName} {selectedUser.lastName}
                            </Text>
                            <Text style={styles.selectedUserEmail} allowFontScaling={false}>
                              {selectedUser.email}
                            </Text>
                            <TouchableOpacity
                              onPress={() => setSelectedUser(null)}
                              style={styles.changeButton}
                            >
                              <Text style={styles.changeButtonText} allowFontScaling={false}>
                                Change
                              </Text>
                            </TouchableOpacity>
                          </View>
                        </View>

                        {/* Position Selection - always required */}
                        <View style={styles.positionSection}>
                          <Text style={styles.sectionLabel} allowFontScaling={false}>
                            Select Position
                          </Text>
                          <View style={styles.positionButtons}>
                            <TouchableOpacity
                              style={[
                                styles.positionButton,
                                selectedPosition === 'Goalie' && styles.positionButtonSelected,
                              ]}
                              onPress={() => setSelectedPosition('Goalie')}
                            >
                              <Text
                                style={[
                                  styles.positionButtonText,
                                  selectedPosition === 'Goalie' && styles.positionButtonTextSelected,
                                ]}
                                allowFontScaling={false}
                              >
                                Goalie
                              </Text>
                            </TouchableOpacity>
                            <TouchableOpacity
                              style={[
                                styles.positionButton,
                                selectedPosition === 'Skater' && styles.positionButtonSelected,
                              ]}
                              onPress={() => setSelectedPosition('Skater')}
                            >
                              <Text
                                style={[
                                  styles.positionButtonText,
                                  selectedPosition === 'Skater' && styles.positionButtonTextSelected,
                                ]}
                                allowFontScaling={false}
                              >
                                Skater
                              </Text>
                            </TouchableOpacity>
                          </View>
                        </View>

                        {/* Confirm Button */}
                        <TouchableOpacity
                          style={[
                            styles.confirmButton,
                            !selectedPosition && styles.confirmButtonDisabled,
                          ]}
                          onPress={handleConfirmAdd}
                          disabled={isAdding || !selectedPosition}
                        >
                          {isAdding ? (
                            <ActivityIndicator size="small" color={colors.text.primary} />
                          ) : (
                            <Text style={styles.confirmButtonText} allowFontScaling={false}>
                              Add Player
                            </Text>
                          )}
                        </TouchableOpacity>
                      </>
                    )}
                  </>
                ) : (
                  /* Create Guest Tab */
                  <ScrollView
                    style={styles.createGuestContainer}
                    contentContainerStyle={styles.createGuestContent}
                    keyboardShouldPersistTaps="handled"
                  >
                    {/* First Name Input */}
                    <View style={styles.inputSection}>
                      <Text style={styles.sectionLabel} allowFontScaling={false}>
                        First Name *
                      </Text>
                      <TextInput
                        style={styles.textInput}
                        placeholder="Enter first name"
                        placeholderTextColor={colors.text.muted}
                        value={guestFirstName}
                        onChangeText={setGuestFirstName}
                        autoCapitalize="words"
                        autoCorrect={false}
                        allowFontScaling={false}
                      />
                    </View>

                    {/* Last Name Input */}
                    <View style={styles.inputSection}>
                      <Text style={styles.sectionLabel} allowFontScaling={false}>
                        Last Name *
                      </Text>
                      <TextInput
                        style={styles.textInput}
                        placeholder="Enter last name"
                        placeholderTextColor={colors.text.muted}
                        value={guestLastName}
                        onChangeText={setGuestLastName}
                        autoCapitalize="words"
                        autoCorrect={false}
                        allowFontScaling={false}
                      />
                    </View>

                    {/* Position Selector */}
                    <View style={styles.inputSection}>
                      <Text style={styles.sectionLabel} allowFontScaling={false}>
                        Position *
                      </Text>
                      <View style={styles.positionButtons}>
                        <TouchableOpacity
                          style={[
                            styles.positionButton,
                            guestPosition === 'Goalie' && styles.positionButtonSelected,
                          ]}
                          onPress={() => setGuestPosition('Goalie')}
                        >
                          <Text
                            style={[
                              styles.positionButtonText,
                              guestPosition === 'Goalie' && styles.positionButtonTextSelected,
                            ]}
                            allowFontScaling={false}
                          >
                            Goalie
                          </Text>
                        </TouchableOpacity>
                        <TouchableOpacity
                          style={[
                            styles.positionButton,
                            guestPosition === 'Skater' && styles.positionButtonSelected,
                          ]}
                          onPress={() => setGuestPosition('Skater')}
                        >
                          <Text
                            style={[
                              styles.positionButtonText,
                              guestPosition === 'Skater' && styles.positionButtonTextSelected,
                            ]}
                            allowFontScaling={false}
                          >
                            Skater
                          </Text>
                        </TouchableOpacity>
                      </View>
                    </View>

                    {/* Skill Level Selector (Optional) */}
                    <View style={styles.inputSection}>
                      <Text style={styles.sectionLabel} allowFontScaling={false}>
                        Skill Level (Optional)
                      </Text>
                      <View style={styles.skillLevelButtons}>
                        {(['Gold', 'Silver', 'Bronze', 'D-League'] as SkillLevel[]).map((level) => (
                          <TouchableOpacity
                            key={level}
                            style={[
                              styles.skillLevelButton,
                              guestSkillLevel === level && styles.skillLevelButtonSelected,
                            ]}
                            onPress={() => setGuestSkillLevel(guestSkillLevel === level ? null : level)}
                          >
                            <Text
                              style={[
                                styles.skillLevelButtonText,
                                guestSkillLevel === level && styles.skillLevelButtonTextSelected,
                              ]}
                              allowFontScaling={false}
                            >
                              {level}
                            </Text>
                          </TouchableOpacity>
                        ))}
                      </View>
                    </View>

                    {/* Create Guest Button */}
                    <TouchableOpacity
                      style={[
                        styles.confirmButton,
                        (!guestFirstName.trim() || !guestLastName.trim() || !guestPosition) && styles.confirmButtonDisabled,
                      ]}
                      onPress={handleCreateGuest}
                      disabled={isCreatingGuest || !guestFirstName.trim() || !guestLastName.trim() || !guestPosition}
                    >
                      {isCreatingGuest ? (
                        <ActivityIndicator size="small" color={colors.text.primary} />
                      ) : (
                        <Text style={styles.confirmButtonText} allowFontScaling={false}>
                          Create Guest
                        </Text>
                      )}
                    </TouchableOpacity>
                  </ScrollView>
                )}
              </View>
            </TouchableWithoutFeedback>
          </KeyboardAvoidingView>
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
  keyboardView: {
    flex: 1,
    justifyContent: 'flex-end',
  },
  modal: {
    backgroundColor: colors.bg.dark,
    borderTopLeftRadius: radius.xl,
    borderTopRightRadius: radius.xl,
    height: '90%',
    paddingBottom: spacing.xl + 20, // Extra padding for home indicator
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  tabContainer: {
    flexDirection: 'row',
    marginHorizontal: spacing.lg,
    marginTop: spacing.md,
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: 4,
  },
  tab: {
    flex: 1,
    paddingVertical: spacing.sm,
    alignItems: 'center',
    borderRadius: radius.sm,
  },
  tabActive: {
    backgroundColor: colors.primary.teal,
  },
  tabText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.muted,
  },
  tabTextActive: {
    color: colors.text.primary,
  },
  title: {
    fontSize: 18,
    fontWeight: '700',
    color: colors.text.primary,
  },
  closeButton: {
    padding: spacing.xs,
  },
  closeButtonText: {
    fontSize: 16,
    color: colors.primary.teal,
    fontWeight: '600',
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    marginHorizontal: spacing.lg,
    marginVertical: spacing.md,
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  searchInput: {
    flex: 1,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.md,
    fontSize: 16,
    color: colors.text.primary,
  },
  searchSpinner: {
    marginRight: spacing.md,
  },
  resultsList: {
    maxHeight: 300,
  },
  resultsContent: {
    paddingHorizontal: spacing.lg,
    paddingBottom: spacing.md,
  },
  userItem: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.sm,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  userItemSelected: {
    borderColor: colors.primary.teal,
    backgroundColor: colors.subtle.teal,
  },
  userInfo: {
    flex: 1,
  },
  userName: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
  userEmail: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: 2,
  },
  positionBadge: {
    backgroundColor: colors.bg.darkest,
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: radius.sm,
  },
  positionText: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.secondary,
  },
  emptyState: {
    padding: spacing.xl,
    alignItems: 'center',
  },
  emptyText: {
    fontSize: 14,
    color: colors.text.muted,
  },
  selectedSection: {
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.md,
  },
  sectionLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.subtle,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.sm,
  },
  selectedUserCard: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.primary.teal,
  },
  selectedUserName: {
    fontSize: 18,
    fontWeight: '700',
    color: colors.text.primary,
  },
  selectedUserEmail: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: 2,
  },
  changeButton: {
    marginTop: spacing.sm,
  },
  changeButtonText: {
    fontSize: 14,
    color: colors.primary.teal,
    fontWeight: '600',
  },
  positionSection: {
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.lg,
  },
  positionButtons: {
    flexDirection: 'row',
    gap: spacing.sm,
  },
  positionButton: {
    flex: 1,
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  positionButtonSelected: {
    borderColor: colors.primary.teal,
    backgroundColor: colors.subtle.teal,
  },
  positionButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.secondary,
  },
  positionButtonTextSelected: {
    color: colors.primary.teal,
  },
  confirmButton: {
    backgroundColor: colors.primary.teal,
    marginHorizontal: spacing.lg,
    marginTop: spacing.lg,
    paddingVertical: spacing.md,
    borderRadius: radius.md,
    alignItems: 'center',
  },
  confirmButtonDisabled: {
    opacity: 0.5,
  },
  confirmButtonText: {
    fontSize: 16,
    fontWeight: '700',
    color: colors.text.primary,
  },
  // Create Guest styles
  createGuestContainer: {
    flex: 1,
  },
  createGuestContent: {
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.md,
    paddingBottom: spacing.xl,
  },
  inputSection: {
    marginTop: spacing.md,
  },
  textInput: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    fontSize: 16,
    color: colors.text.primary,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  skillLevelButtons: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.sm,
  },
  skillLevelButton: {
    flex: 1,
    minWidth: '45%',
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  skillLevelButtonSelected: {
    borderColor: colors.primary.teal,
    backgroundColor: colors.subtle.teal,
  },
  skillLevelButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.secondary,
  },
  skillLevelButtonTextSelected: {
    color: colors.primary.teal,
  },
});
