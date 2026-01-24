import { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
} from 'react-native';
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { useTournamentStore } from '../../../../stores/tournamentStore';
import { colors, spacing, radius } from '../../../../theme';

interface ParticipationOptionProps {
  emoji: string;
  title: string;
  description: string;
  onPress: () => void;
}

function ParticipationOption({ emoji, title, description, onPress }: ParticipationOptionProps) {
  return (
    <TouchableOpacity style={styles.optionCard} onPress={onPress} activeOpacity={0.7}>
      <View style={styles.optionIconContainer}>
        <Text style={styles.optionEmoji}>{emoji}</Text>
      </View>
      <View style={styles.optionContent}>
        <Text style={styles.optionTitle}>{title}</Text>
        <Text style={styles.optionDescription}>{description}</Text>
      </View>
    </TouchableOpacity>
  );
}

export default function TournamentRegistrationEntryScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();

  const currentTournament = useTournamentStore(state => state.currentTournament);
  const isLoading = useTournamentStore(state => state.isLoading);
  const fetchTournamentById = useTournamentStore(state => state.fetchTournamentById);

  // Fetch tournament data on mount if not already loaded
  useEffect(() => {
    if (id && (!currentTournament || currentTournament.id !== id)) {
      fetchTournamentById(id);
    }
  }, [id]);

  const handleCreateTeam = () => {
    router.push(`/tournaments/${id}/register/captain`);
  };

  const handleJoinAsFreeAgent = () => {
    router.push(`/tournaments/${id}/register`);
  };

  const handleBrowseTeams = () => {
    router.push(`/tournaments/${id}/register/browse`);
  };

  return (
    <View style={styles.container}>
      <Stack.Screen
        options={{
          title: 'How to Participate',
          headerStyle: {
            backgroundColor: colors.bg.dark,
          },
          headerTintColor: colors.text.primary,
          headerShadowVisible: false,
        }}
      />

      {isLoading && !currentTournament ? (
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
        </View>
      ) : (
        <ScrollView
          style={styles.content}
          contentContainerStyle={styles.contentContainer}
          showsVerticalScrollIndicator={false}
        >
          <View style={styles.header}>
            <Text style={styles.headerTitle}>How do you want to participate?</Text>
            <Text style={styles.headerSubtitle}>
              Choose the registration option that works best for you
            </Text>
          </View>

          <View style={styles.optionsContainer}>
            <ParticipationOption
              emoji="👥"
              title="Create a Team"
              description="Become a captain and build your team"
              onPress={handleCreateTeam}
            />

            <ParticipationOption
              emoji="🙋"
              title="Join as Free Agent"
              description="Register solo and wait for team assignment"
              onPress={handleJoinAsFreeAgent}
            />

            <ParticipationOption
              emoji="🔍"
              title="Browse Teams"
              description="View teams looking for players"
              onPress={handleBrowseTeams}
            />
          </View>
        </ScrollView>
      )}
    </View>
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
  content: {
    flex: 1,
  },
  contentContainer: {
    paddingHorizontal: spacing.md,
    paddingTop: spacing.lg,
    paddingBottom: spacing.xxl,
  },
  header: {
    marginBottom: spacing.xl,
  },
  headerTitle: {
    fontSize: 24,
    fontWeight: '700',
    color: colors.text.primary,
    marginBottom: spacing.sm,
  },
  headerSubtitle: {
    fontSize: 15,
    color: colors.text.muted,
    lineHeight: 20,
  },
  optionsContainer: {
    gap: spacing.md,
  },
  optionCard: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  optionIconContainer: {
    width: 56,
    height: 56,
    borderRadius: radius.lg,
    backgroundColor: colors.bg.elevated,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: spacing.md,
  },
  optionEmoji: {
    fontSize: 28,
  },
  optionContent: {
    gap: spacing.xs,
  },
  optionTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: 2,
  },
  optionDescription: {
    fontSize: 14,
    color: colors.text.muted,
    lineHeight: 20,
  },
});
