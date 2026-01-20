import { View, Text, StyleSheet, ScrollView, TouchableOpacity } from 'react-native';
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing, radius } from '../../../../theme';

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

  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: 'Manage Tournament' }} />

      <ScrollView style={styles.content} showsVerticalScrollIndicator={false}>
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
});
