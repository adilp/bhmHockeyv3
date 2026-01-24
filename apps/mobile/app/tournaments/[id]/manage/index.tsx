import { View, Text, StyleSheet, ScrollView, TouchableOpacity, Alert, ActivityIndicator } from 'react-native';
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { useCallback } from 'react';
import { useFocusEffect } from '@react-navigation/native';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing, radius } from '../../../../theme';
import { useTournamentStore } from '../../../../stores/tournamentStore';

interface ManageOptionProps {
  icon: keyof typeof Ionicons.glyphMap;
  title: string;
  description: string;
  onPress: () => void;
}

function ManageOption({ icon, title, description, onPress }: ManageOptionProps) {
  return (
    <TouchableOpacity style={styles.option} onPress={onPress} activeOpacity={0.7}>
      <View style={styles.optionIcon}>
        <Ionicons name={icon} size={24} color={colors.primary.teal} />
      </View>
      <View style={styles.optionContent}>
        <Text style={styles.optionTitle}>{title}</Text>
        <Text style={styles.optionDescription}>{description}</Text>
      </View>
      <Ionicons name="chevron-forward" size={20} color={colors.text.muted} />
    </TouchableOpacity>
  );
}

export default function ManageTournamentScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();

  const currentTournament = useTournamentStore(state => state.currentTournament);
  const processingId = useTournamentStore(state => state.processingId);
  const fetchTournamentById = useTournamentStore(state => state.fetchTournamentById);
  const publishTournament = useTournamentStore(state => state.publishTournament);
  const deleteTournament = useTournamentStore(state => state.deleteTournament);

  const isPublishing = processingId === `publish-${id}`;
  const isDeleting = processingId === `delete-${id}`;
  const isDraft = currentTournament?.status === 'Draft';

  // Fetch tournament data on focus
  useFocusEffect(
    useCallback(() => {
      if (id) {
        fetchTournamentById(id);
      }
    }, [id])
  );

  const handlePublish = () => {
    Alert.alert(
      'Publish Tournament',
      'This will make your tournament visible to all users and open registration. Are you sure?',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Publish',
          style: 'default',
          onPress: async () => {
            if (!id) return;
            const success = await publishTournament(id);
            if (success) {
              Alert.alert('Success', 'Tournament published! Registration is now open.');
            } else {
              Alert.alert('Error', 'Failed to publish tournament. Please try again.');
            }
          },
        },
      ]
    );
  };

  const handleDelete = () => {
    Alert.alert(
      'Delete Tournament',
      'Are you sure you want to delete this tournament? This action cannot be undone.',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Delete',
          style: 'destructive',
          onPress: async () => {
            if (!id) return;
            const success = await deleteTournament(id);
            if (success) {
              Alert.alert('Deleted', 'Tournament has been deleted.', [
                { text: 'OK', onPress: () => router.replace('/tournaments') },
              ]);
            } else {
              Alert.alert('Error', 'Failed to delete tournament. Only Draft tournaments can be deleted.');
            }
          },
        },
      ]
    );
  };

  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: 'Manage Tournament' }} />

      <ScrollView style={styles.content} showsVerticalScrollIndicator={false}>
        {/* Draft Status Banner with Publish Button */}
        {isDraft && (
          <View style={styles.draftBanner}>
            <View style={styles.draftInfo}>
              <Ionicons name="eye-off" size={20} color={colors.status.warning} />
              <View style={styles.draftTextContainer}>
                <Text style={styles.draftTitle}>Draft Mode</Text>
                <Text style={styles.draftDescription}>
                  This tournament is only visible to you. Publish to open registration.
                </Text>
              </View>
            </View>
            <TouchableOpacity
              style={[styles.publishButton, isPublishing && styles.publishButtonDisabled]}
              onPress={handlePublish}
              disabled={isPublishing}
              activeOpacity={0.7}
            >
              {isPublishing ? (
                <ActivityIndicator size="small" color={colors.bg.darkest} />
              ) : (
                <>
                  <Ionicons name="rocket" size={18} color={colors.bg.darkest} />
                  <Text style={styles.publishButtonText}>Publish</Text>
                </>
              )}
            </TouchableOpacity>
          </View>
        )}

        {/* Tournament Settings Section */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Tournament</Text>

          <ManageOption
            icon="settings-outline"
            title="Settings"
            description="Edit tournament details, dates, and rules"
            onPress={() => router.push(`/tournaments/${id}/manage/settings`)}
          />

          <ManageOption
            icon="help-circle-outline"
            title="Custom Questions"
            description="Manage registration questions and waivers"
            onPress={() => router.push(`/tournaments/${id}/manage/questions`)}
          />
        </View>

        {/* People Section */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>People</Text>

          <ManageOption
            icon="people-outline"
            title="Registrations"
            description="View and manage player registrations"
            onPress={() => router.push(`/tournaments/${id}/manage/registrations`)}
          />

          <ManageOption
            icon="shirt-outline"
            title="Teams"
            description="Manage teams and player assignments"
            onPress={() => router.push(`/tournaments/${id}/manage/teams`)}
          />

          <ManageOption
            icon="shield-outline"
            title="Admins"
            description="Manage tournament administrators"
            onPress={() => router.push(`/tournaments/${id}/manage/admins`)}
          />
        </View>

        {/* Competition Section */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Competition</Text>

          <ManageOption
            icon="podium-outline"
            title="Standings"
            description="View standings and resolve ties"
            onPress={() => router.push(`/tournaments/${id}/manage/standings`)}
          />

          <ManageOption
            icon="create-outline"
            title="Bracket & Scores"
            description="View bracket and enter match scores"
            onPress={() => router.push(`/tournaments/${id}/bracket`)}
          />
        </View>

        {/* Communication Section */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Communication</Text>

          <ManageOption
            icon="megaphone-outline"
            title="Announcements"
            description="Send announcements to participants"
            onPress={() => router.push(`/tournaments/${id}/manage/announcements`)}
          />
        </View>

        {/* Activity Section */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Activity</Text>

          <ManageOption
            icon="document-text-outline"
            title="Audit Log"
            description="View tournament activity history"
            onPress={() => router.push(`/tournaments/${id}/manage/audit`)}
          />
        </View>

        {/* Danger Zone Section - Only show for Draft tournaments */}
        {isDraft && (
          <View style={styles.section}>
            <Text style={[styles.sectionTitle, styles.dangerSectionTitle]}>Danger Zone</Text>

            <TouchableOpacity
              style={[styles.deleteButton, isDeleting && styles.deleteButtonDisabled]}
              onPress={handleDelete}
              disabled={isDeleting}
              activeOpacity={0.7}
            >
              {isDeleting ? (
                <ActivityIndicator size="small" color={colors.status.error} />
              ) : (
                <>
                  <Ionicons name="trash-outline" size={20} color={colors.status.error} />
                  <Text style={styles.deleteButtonText}>Delete Tournament</Text>
                </>
              )}
            </TouchableOpacity>
          </View>
        )}

        <View style={styles.bottomPadding} />
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
  content: {
    flex: 1,
  },
  section: {
    marginTop: spacing.lg,
    paddingHorizontal: spacing.md,
  },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.sm,
    marginLeft: spacing.xs,
  },
  option: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    marginBottom: spacing.sm,
  },
  optionIcon: {
    width: 40,
    height: 40,
    borderRadius: radius.md,
    backgroundColor: colors.subtle.teal,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: spacing.md,
  },
  optionContent: {
    flex: 1,
  },
  optionTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
  optionDescription: {
    fontSize: 13,
    color: colors.text.muted,
    marginTop: 2,
  },
  bottomPadding: {
    height: spacing.xxl,
  },
  draftBanner: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    marginHorizontal: spacing.md,
    marginTop: spacing.md,
    borderWidth: 1,
    borderColor: colors.status.warning,
  },
  draftInfo: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    marginBottom: spacing.md,
  },
  draftTextContainer: {
    flex: 1,
    marginLeft: spacing.sm,
  },
  draftTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.status.warning,
  },
  draftDescription: {
    fontSize: 13,
    color: colors.text.secondary,
    marginTop: 2,
  },
  publishButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    paddingVertical: spacing.sm + 2,
    paddingHorizontal: spacing.md,
    gap: spacing.xs,
  },
  publishButtonDisabled: {
    opacity: 0.6,
  },
  publishButtonText: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.bg.darkest,
  },
  dangerSectionTitle: {
    color: colors.status.error,
  },
  deleteButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.status.error,
    gap: spacing.sm,
  },
  deleteButtonDisabled: {
    opacity: 0.6,
  },
  deleteButtonText: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.status.error,
  },
});
