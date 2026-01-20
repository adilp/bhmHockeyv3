import { useState, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  RefreshControl,
  ActivityIndicator,
  TextInput,
  Alert,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { useLocalSearchParams, Stack, useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useShallow } from 'zustand/react/shallow';
import { useTournamentStore } from '../../../../stores/tournamentStore';
import { EmptyState } from '../../../../components';
import { colors, spacing, radius } from '../../../../theme';
import type { TournamentAnnouncementDto, AnnouncementTarget } from '@bhmhockey/shared';

// Target options for announcements
const TARGET_OPTIONS: { label: string; value: AnnouncementTarget }[] = [
  { label: 'Everyone', value: 'All' },
  { label: 'Captains Only', value: 'Captains' },
  { label: 'Admins Only', value: 'Admins' },
];

// Helper to format timestamp
const formatTimestamp = (iso: string) => {
  const date = new Date(iso);
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
};

// Helper to get target label
const getTargetLabel = (target: AnnouncementTarget | null, teamCount?: number) => {
  if (!target && teamCount) return `${teamCount} team${teamCount > 1 ? 's' : ''}`;
  switch (target) {
    case 'All':
      return 'Everyone';
    case 'Captains':
      return 'Captains';
    case 'Admins':
      return 'Admins';
    default:
      return 'Everyone';
  }
};

// Announcement item component
function AnnouncementItem({
  item,
  onDelete,
}: {
  item: TournamentAnnouncementDto;
  onDelete: () => void;
}) {
  return (
    <View style={styles.announcementItem}>
      <View style={styles.announcementHeader}>
        <View style={styles.targetBadge}>
          <Ionicons name="people-outline" size={12} color={colors.primary.teal} />
          <Text style={styles.targetText}>
            {getTargetLabel(item.target, item.targetTeamIds?.length)}
          </Text>
        </View>
        <TouchableOpacity
          onPress={onDelete}
          hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
        >
          <Ionicons name="trash-outline" size={18} color={colors.text.muted} />
        </TouchableOpacity>
      </View>
      <Text style={styles.announcementTitle}>{item.title}</Text>
      <Text style={styles.announcementBody}>{item.body}</Text>
      <Text style={styles.announcementMeta}>
        {item.createdByFirstName} {item.createdByLastName} - {formatTimestamp(item.createdAt)}
      </Text>
    </View>
  );
}

export default function AnnouncementsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [showCompose, setShowCompose] = useState(false);
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [selectedTarget, setSelectedTarget] = useState<AnnouncementTarget>('All');

  const {
    announcements,
    isLoadingAnnouncements,
    isSendingAnnouncement,
    fetchAnnouncements,
    createAnnouncement,
    deleteAnnouncement,
    clearAnnouncements,
  } = useTournamentStore(
    useShallow((state) => ({
      announcements: state.announcements,
      isLoadingAnnouncements: state.isLoadingAnnouncements,
      isSendingAnnouncement: state.isSendingAnnouncement,
      fetchAnnouncements: state.fetchAnnouncements,
      createAnnouncement: state.createAnnouncement,
      deleteAnnouncement: state.deleteAnnouncement,
      clearAnnouncements: state.clearAnnouncements,
    }))
  );

  // Load announcements when screen is focused
  useFocusEffect(
    useCallback(() => {
      if (id) fetchAnnouncements(id);
      return () => {
        clearAnnouncements();
      };
    }, [id, fetchAnnouncements, clearAnnouncements])
  );

  // Handle refresh
  const handleRefresh = useCallback(async () => {
    if (!id) return;
    setIsRefreshing(true);
    await fetchAnnouncements(id);
    setIsRefreshing(false);
  }, [id, fetchAnnouncements]);

  // Handle send announcement
  const handleSend = async () => {
    if (!id || !title.trim() || !body.trim()) {
      Alert.alert('Error', 'Please enter a title and message');
      return;
    }

    const success = await createAnnouncement(id, title.trim(), body.trim(), selectedTarget);
    if (success) {
      setTitle('');
      setBody('');
      setShowCompose(false);
      Alert.alert('Success', 'Announcement sent successfully');
    } else {
      Alert.alert('Error', 'Failed to send announcement');
    }
  };

  // Handle delete announcement
  const handleDelete = (announcementId: string) => {
    Alert.alert(
      'Delete Announcement',
      'Are you sure you want to delete this announcement?',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Delete',
          style: 'destructive',
          onPress: async () => {
            if (!id) return;
            const success = await deleteAnnouncement(id, announcementId);
            if (!success) {
              Alert.alert('Error', 'Failed to delete announcement');
            }
          },
        },
      ]
    );
  };

  // Render announcement item
  const renderItem = ({ item }: { item: TournamentAnnouncementDto }) => (
    <AnnouncementItem item={item} onDelete={() => handleDelete(item.id)} />
  );

  // Loading state
  if (isLoadingAnnouncements && announcements.length === 0) {
    return (
      <View style={styles.container}>
        <Stack.Screen options={{ title: 'Announcements' }} />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
        </View>
      </View>
    );
  }

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <Stack.Screen options={{ title: 'Announcements' }} />

      {/* Compose Section */}
      {showCompose ? (
        <View style={styles.composeContainer}>
          <View style={styles.composeHeader}>
            <Text style={styles.composeTitle}>New Announcement</Text>
            <TouchableOpacity onPress={() => setShowCompose(false)}>
              <Ionicons name="close" size={24} color={colors.text.secondary} />
            </TouchableOpacity>
          </View>

          {/* Target Selector */}
          <View style={styles.targetSelector}>
            {TARGET_OPTIONS.map((option) => (
              <TouchableOpacity
                key={option.value}
                style={[
                  styles.targetOption,
                  selectedTarget === option.value && styles.targetOptionSelected,
                ]}
                onPress={() => setSelectedTarget(option.value)}
              >
                <Text
                  style={[
                    styles.targetOptionText,
                    selectedTarget === option.value && styles.targetOptionTextSelected,
                  ]}
                >
                  {option.label}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          {/* Title Input */}
          <TextInput
            style={styles.titleInput}
            placeholder="Title"
            placeholderTextColor={colors.text.muted}
            value={title}
            onChangeText={setTitle}
            maxLength={100}
          />

          {/* Body Input */}
          <TextInput
            style={styles.bodyInput}
            placeholder="Write your announcement..."
            placeholderTextColor={colors.text.muted}
            value={body}
            onChangeText={setBody}
            multiline
            numberOfLines={4}
            maxLength={1000}
            textAlignVertical="top"
          />

          {/* Send Button */}
          <TouchableOpacity
            style={[styles.sendButton, isSendingAnnouncement && styles.sendButtonDisabled]}
            onPress={handleSend}
            disabled={isSendingAnnouncement}
          >
            {isSendingAnnouncement ? (
              <ActivityIndicator size="small" color={colors.bg.darkest} />
            ) : (
              <>
                <Ionicons name="send" size={18} color={colors.bg.darkest} />
                <Text style={styles.sendButtonText}>Send Announcement</Text>
              </>
            )}
          </TouchableOpacity>
        </View>
      ) : (
        <TouchableOpacity style={styles.composeButton} onPress={() => setShowCompose(true)}>
          <Ionicons name="add" size={20} color={colors.bg.darkest} />
          <Text style={styles.composeButtonText}>New Announcement</Text>
        </TouchableOpacity>
      )}

      {/* Announcements List */}
      <FlatList
        data={announcements}
        renderItem={renderItem}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.listContent}
        refreshControl={
          <RefreshControl
            refreshing={isRefreshing}
            onRefresh={handleRefresh}
            tintColor={colors.primary.teal}
            colors={[colors.primary.teal]}
          />
        }
        ListEmptyComponent={
          <EmptyState
            icon="megaphone-outline"
            title="No Announcements"
            message="Send announcements to keep participants informed"
          />
        }
      />
    </KeyboardAvoidingView>
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
  },
  listContent: {
    padding: spacing.md,
    paddingTop: spacing.sm,
    flexGrow: 1,
  },

  // Compose button (collapsed)
  composeButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.primary.teal,
    margin: spacing.md,
    marginBottom: 0,
    padding: spacing.md,
    borderRadius: radius.md,
    gap: spacing.sm,
  },
  composeButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.bg.darkest,
  },

  // Compose container (expanded)
  composeContainer: {
    backgroundColor: colors.bg.dark,
    margin: spacing.md,
    marginBottom: 0,
    padding: spacing.md,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  composeHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.md,
  },
  composeTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
  },

  // Target selector
  targetSelector: {
    flexDirection: 'row',
    gap: spacing.sm,
    marginBottom: spacing.md,
  },
  targetOption: {
    flex: 1,
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.sm,
    borderRadius: radius.md,
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.default,
    alignItems: 'center',
  },
  targetOptionSelected: {
    backgroundColor: colors.subtle.teal,
    borderColor: colors.primary.teal,
  },
  targetOptionText: {
    fontSize: 13,
    fontWeight: '500',
    color: colors.text.secondary,
  },
  targetOptionTextSelected: {
    color: colors.primary.teal,
  },

  // Input fields
  titleInput: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    fontSize: 16,
    color: colors.text.primary,
    marginBottom: spacing.sm,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  bodyInput: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    fontSize: 15,
    color: colors.text.primary,
    minHeight: 100,
    marginBottom: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },

  // Send button
  sendButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.primary.teal,
    padding: spacing.md,
    borderRadius: radius.md,
    gap: spacing.sm,
  },
  sendButtonDisabled: {
    opacity: 0.6,
  },
  sendButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.bg.darkest,
  },

  // Announcement item
  announcementItem: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    marginBottom: spacing.sm,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  announcementHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  targetBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.subtle.teal,
    paddingVertical: 4,
    paddingHorizontal: spacing.sm,
    borderRadius: radius.sm,
    gap: 4,
  },
  targetText: {
    fontSize: 12,
    fontWeight: '500',
    color: colors.primary.teal,
  },
  announcementTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: spacing.xs,
  },
  announcementBody: {
    fontSize: 14,
    color: colors.text.secondary,
    lineHeight: 20,
    marginBottom: spacing.sm,
  },
  announcementMeta: {
    fontSize: 12,
    color: colors.text.muted,
  },
});
