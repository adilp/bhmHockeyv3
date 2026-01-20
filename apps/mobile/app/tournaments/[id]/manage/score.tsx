import { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
} from 'react-native';
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useShallow } from 'zustand/react/shallow';

import { useTournamentStore } from '../../../../stores/tournamentStore';
import { colors, spacing, radius } from '../../../../theme';

interface ScoreRowProps {
  teamName: string;
  score: number;
  onIncrement: () => void;
  onDecrement: () => void;
}

/**
 * Score stepper row component for each team
 */
function ScoreRow({ teamName, score, onIncrement, onDecrement }: ScoreRowProps) {
  return (
    <View style={styles.scoreRow}>
      <Text style={styles.teamName} numberOfLines={2}>
        {teamName}
      </Text>
      <View style={styles.stepper}>
        <TouchableOpacity
          onPress={onDecrement}
          style={styles.stepperButton}
          activeOpacity={0.7}
        >
          <Text style={styles.stepperText}>-</Text>
        </TouchableOpacity>
        <View style={styles.scoreValueContainer}>
          <Text style={styles.scoreValue}>{score}</Text>
        </View>
        <TouchableOpacity
          onPress={onIncrement}
          style={styles.stepperButton}
          activeOpacity={0.7}
        >
          <Text style={styles.stepperText}>+</Text>
        </TouchableOpacity>
      </View>
    </View>
  );
}

/**
 * Score Entry Screen
 * Allows entering/editing scores for tournament matches
 */
export default function ScoreEntryScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();

  // Get matchId from search params
  const searchParams = useLocalSearchParams();
  const matchId = searchParams.matchId as string;

  const [homeScore, setHomeScore] = useState(0);
  const [awayScore, setAwayScore] = useState(0);
  const [isSaving, setIsSaving] = useState(false);

  const { matches, enterScore, currentTournament, error, clearError } = useTournamentStore(
    useShallow((state) => ({
      matches: state.matches,
      enterScore: state.enterScore,
      currentTournament: state.currentTournament,
      error: state.error,
      clearError: state.clearError,
    }))
  );

  // Find the match
  const match = matches.find((m) => m.id === matchId);

  // Initialize scores from existing match data
  useEffect(() => {
    if (match) {
      setHomeScore(match.homeScore ?? 0);
      setAwayScore(match.awayScore ?? 0);
    }
  }, [match]);

  // Show error if present
  useEffect(() => {
    if (error) {
      Alert.alert('Error', error, [{ text: 'OK', onPress: clearError }]);
    }
  }, [error, clearError]);

  const handleSave = async () => {
    if (!id || !matchId) return;

    // Prevent ties in elimination matches (can be extended for overtime handling)
    if (homeScore === awayScore) {
      Alert.alert(
        'Tied Score',
        'Elimination matches cannot end in a tie. Please enter a valid score.',
        [{ text: 'OK' }]
      );
      return;
    }

    setIsSaving(true);
    const success = await enterScore(id, matchId, homeScore, awayScore);
    setIsSaving(false);

    if (success) {
      router.back();
    }
  };

  const handleClose = () => {
    router.back();
  };

  // If match not found, show error state
  if (!match) {
    return (
      <View style={styles.container}>
        <Stack.Screen options={{ title: 'Enter Score' }} />
        <View style={styles.errorContainer}>
          <Ionicons name="alert-circle-outline" size={48} color={colors.status.error} />
          <Text style={styles.errorText}>Match not found</Text>
          <TouchableOpacity style={styles.backButton} onPress={handleClose}>
            <Text style={styles.backButtonText}>Go Back</Text>
          </TouchableOpacity>
        </View>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: 'Enter Score' }} />

      {/* Match Info */}
      <View style={styles.matchInfo}>
        <Text style={styles.matchTitle}>Match {match.matchNumber}</Text>
        {currentTournament && (
          <Text style={styles.tournamentName} numberOfLines={1}>
            {currentTournament.name}
          </Text>
        )}
      </View>

      {/* Score Entry */}
      <View style={styles.scoreContainer}>
        {/* Home Team */}
        <ScoreRow
          teamName={match.homeTeamName ?? 'Home'}
          score={homeScore}
          onIncrement={() => setHomeScore((s) => s + 1)}
          onDecrement={() => setHomeScore((s) => Math.max(0, s - 1))}
        />

        {/* VS Divider */}
        <View style={styles.vsDivider}>
          <Text style={styles.vsText}>VS</Text>
        </View>

        {/* Away Team */}
        <ScoreRow
          teamName={match.awayTeamName ?? 'Away'}
          score={awayScore}
          onIncrement={() => setAwayScore((s) => s + 1)}
          onDecrement={() => setAwayScore((s) => Math.max(0, s - 1))}
        />
      </View>

      {/* Winner Preview */}
      {homeScore !== awayScore && (
        <View style={styles.winnerPreview}>
          <Ionicons name="trophy" size={16} color={colors.primary.teal} />
          <Text style={styles.winnerText}>
            Winner: {homeScore > awayScore ? match.homeTeamName : match.awayTeamName}
          </Text>
        </View>
      )}

      {/* Action Buttons */}
      <View style={styles.buttonRow}>
        <TouchableOpacity
          onPress={handleClose}
          style={styles.cancelButton}
          activeOpacity={0.7}
          disabled={isSaving}
        >
          <Text style={styles.cancelButtonText}>Cancel</Text>
        </TouchableOpacity>

        <TouchableOpacity
          onPress={handleSave}
          style={[styles.saveButton, isSaving && styles.saveButtonDisabled]}
          activeOpacity={0.7}
          disabled={isSaving}
        >
          {isSaving ? (
            <ActivityIndicator size="small" color={colors.bg.darkest} />
          ) : (
            <Text style={styles.saveButtonText}>Save Score</Text>
          )}
        </TouchableOpacity>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },

  // Match info styles
  matchInfo: {
    alignItems: 'center',
    paddingVertical: spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  matchTitle: {
    fontSize: 22,
    fontWeight: '700',
    color: colors.text.primary,
    marginBottom: spacing.xs,
  },
  tournamentName: {
    fontSize: 14,
    color: colors.text.muted,
  },

  // Score container styles
  scoreContainer: {
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.xl,
  },

  // Score row styles
  scoreRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: spacing.md,
  },
  teamName: {
    flex: 1,
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
    marginRight: spacing.md,
  },
  stepper: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  stepperButton: {
    width: 56,
    height: 56,
    justifyContent: 'center',
    alignItems: 'center',
  },
  stepperText: {
    fontSize: 28,
    fontWeight: '600',
    color: colors.primary.teal,
  },
  scoreValueContainer: {
    width: 64,
    height: 56,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.dark,
    borderLeftWidth: 1,
    borderRightWidth: 1,
    borderColor: colors.border.default,
  },
  scoreValue: {
    fontSize: 28,
    fontWeight: '700',
    color: colors.text.primary,
  },

  // VS Divider styles
  vsDivider: {
    alignItems: 'center',
    paddingVertical: spacing.md,
  },
  vsText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.muted,
    letterSpacing: 1,
  },

  // Winner preview styles
  winnerPreview: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    backgroundColor: colors.subtle.teal,
    marginHorizontal: spacing.lg,
    borderRadius: radius.md,
    gap: spacing.sm,
  },
  winnerText: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.primary.teal,
  },

  // Button row styles
  buttonRow: {
    flexDirection: 'row',
    gap: spacing.md,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.xl,
    marginTop: 'auto',
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },
  cancelButton: {
    flex: 1,
    paddingVertical: 16,
    borderRadius: radius.md,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.muted,
  },
  cancelButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.secondary,
  },
  saveButton: {
    flex: 1,
    paddingVertical: 16,
    borderRadius: radius.md,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.primary.teal,
  },
  saveButtonDisabled: {
    opacity: 0.6,
  },
  saveButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.bg.darkest,
  },

  // Error state styles
  errorContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: spacing.xl,
  },
  errorText: {
    fontSize: 16,
    color: colors.text.muted,
    marginTop: spacing.md,
    marginBottom: spacing.lg,
  },
  backButton: {
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  backButtonText: {
    fontSize: 14,
    fontWeight: '500',
    color: colors.primary.teal,
  },
});
