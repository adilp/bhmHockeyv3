import { useCallback, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ActivityIndicator,
  ScrollView,
  TouchableOpacity,
  RefreshControl,
  Alert,
} from 'react-native';
import { useLocalSearchParams, useRouter, useFocusEffect, Stack } from 'expo-router';
import { useTournamentStore } from '../../../stores/tournamentStore';
import { useAuthStore } from '../../../stores/authStore';
import { TournamentStatusBadge, Badge, RegistrationStatusSheet } from '../../../components';
import { colors, spacing, radius } from '../../../theme';
import type { TournamentDto, TournamentFormat } from '@bhmhockey/shared';

type TabKey = 'info' | 'bracket' | 'teams';

// Format display labels
const formatLabels: Record<TournamentFormat, string> = {
  SingleElimination: 'Single Elimination',
  DoubleElimination: 'Double Elimination',
  RoundRobin: 'Round Robin',
};

// Format date for display
function formatDate(dateString: string): string {
  const date = new Date(dateString);
  return date.toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

// Format date range
function formatDateRange(startDate: string, endDate: string): string {
  const start = new Date(startDate);
  const end = new Date(endDate);

  // If same day, show single date
  if (start.toDateString() === end.toDateString()) {
    return formatDate(startDate);
  }

  // If same year, omit year from start date
  if (start.getFullYear() === end.getFullYear()) {
    const startFormatted = start.toLocaleDateString('en-US', {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
    });
    const endFormatted = end.toLocaleDateString('en-US', {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
    return `${startFormatted} - ${endFormatted}`;
  }

  // Different years, show full dates
  return `${formatDate(startDate)} - ${formatDate(endDate)}`;
}

// Info Tab Component
interface InfoTabProps {
  tournament: TournamentDto;
  isRefreshing: boolean;
  onRefresh: () => void;
  myRegistration: any;
  onShowStatusSheet: () => void;
}

function InfoTab({ tournament, isRefreshing, onRefresh, myRegistration, onShowStatusSheet }: InfoTabProps) {
  // Map registration status to badge variant and display text
  const getRegistrationStatusConfig = (status: string, isWaitlisted: boolean): { variant: 'green' | 'warning' | 'error' | 'default'; label: string } => {
    if (isWaitlisted) {
      return { variant: 'warning', label: 'Waitlisted' };
    }
    switch (status) {
      case 'Registered':
        return { variant: 'green', label: 'Registered' };
      case 'Cancelled':
        return { variant: 'error', label: 'Cancelled' };
      default:
        return { variant: 'default', label: status };
    }
  };

  return (
    <ScrollView
      style={styles.scrollView}
      contentContainerStyle={styles.scrollContent}
      refreshControl={
        <RefreshControl
          refreshing={isRefreshing}
          onRefresh={onRefresh}
          tintColor={colors.primary.teal}
        />
      }
    >
      {/* Format Badge */}
      <View style={styles.infoSection}>
        <Badge variant="purple">{formatLabels[tournament.format]}</Badge>
      </View>

      {/* Date Range */}
      <View style={styles.infoSection}>
        <Text style={styles.sectionLabel}>DATES</Text>
        <Text style={styles.sectionValue}>
          {formatDateRange(tournament.startDate, tournament.endDate)}
        </Text>
      </View>

      {/* Registration Deadline */}
      <View style={styles.infoSection}>
        <Text style={styles.sectionLabel}>REGISTRATION DEADLINE</Text>
        <Text style={styles.sectionValue}>{formatDate(tournament.registrationDeadline)}</Text>
      </View>

      {/* Venue */}
      {tournament.venue && (
        <View style={styles.infoSection}>
          <Text style={styles.sectionLabel}>VENUE</Text>
          <Text style={styles.sectionValue}>{tournament.venue}</Text>
        </View>
      )}

      {/* Description */}
      {tournament.description && (
        <View style={styles.infoSection}>
          <Text style={styles.sectionLabel}>DESCRIPTION</Text>
          <Text style={styles.sectionValueMultiline}>{tournament.description}</Text>
        </View>
      )}

      {/* Entry Fee */}
      <View style={styles.infoSection}>
        <Text style={styles.sectionLabel}>ENTRY FEE</Text>
        <Text style={styles.sectionValue}>
          {tournament.entryFee > 0
            ? `$${tournament.entryFee}${tournament.feeType === 'PerTeam' ? ' per team' : ' per player'}`
            : 'Free'}
        </Text>
      </View>

      {/* Team Configuration */}
      <View style={styles.infoSection}>
        <Text style={styles.sectionLabel}>TEAM CONFIGURATION</Text>
        <View style={styles.configRow}>
          <Text style={styles.configLabel}>Max Teams:</Text>
          <Text style={styles.configValue}>{tournament.maxTeams}</Text>
        </View>
        {tournament.minPlayersPerTeam && (
          <View style={styles.configRow}>
            <Text style={styles.configLabel}>Min Players per Team:</Text>
            <Text style={styles.configValue}>{tournament.minPlayersPerTeam}</Text>
          </View>
        )}
        {tournament.maxPlayersPerTeam && (
          <View style={styles.configRow}>
            <Text style={styles.configLabel}>Max Players per Team:</Text>
            <Text style={styles.configValue}>{tournament.maxPlayersPerTeam}</Text>
          </View>
        )}
        <View style={styles.configRow}>
          <Text style={styles.configLabel}>Team Formation:</Text>
          <Text style={styles.configValue}>
            {tournament.teamFormation === 'PreFormed' ? 'Pre-Formed Teams' : 'Organizer Assigned'}
          </Text>
        </View>
        <View style={styles.configRow}>
          <Text style={styles.configLabel}>Substitutions:</Text>
          <Text style={styles.configValue}>
            {tournament.allowSubstitutions ? 'Allowed' : 'Not Allowed'}
          </Text>
        </View>
      </View>

      {/* Round Robin Scoring (if applicable) */}
      {tournament.format === 'RoundRobin' && (
        <View style={styles.infoSection}>
          <Text style={styles.sectionLabel}>SCORING</Text>
          <View style={styles.configRow}>
            <Text style={styles.configLabel}>Win:</Text>
            <Text style={styles.configValue}>{tournament.pointsWin} pts</Text>
          </View>
          <View style={styles.configRow}>
            <Text style={styles.configLabel}>Tie:</Text>
            <Text style={styles.configValue}>{tournament.pointsTie} pts</Text>
          </View>
          <View style={styles.configRow}>
            <Text style={styles.configLabel}>Loss:</Text>
            <Text style={styles.configValue}>{tournament.pointsLoss} pts</Text>
          </View>
        </View>
      )}

      {/* Rules */}
      {tournament.rulesContent && (
        <View style={styles.infoSection}>
          <Text style={styles.sectionLabel}>RULES</Text>
          <Text style={styles.sectionValueMultiline}>{tournament.rulesContent}</Text>
        </View>
      )}

      {/* Your Registration Section */}
      {myRegistration && myRegistration.status !== 'Cancelled' && (
        <View style={styles.infoSection}>
          <Text style={styles.sectionLabel}>YOUR REGISTRATION</Text>
          <TouchableOpacity
            style={styles.registrationCard}
            onPress={onShowStatusSheet}
            activeOpacity={0.7}
          >
            <View style={styles.registrationHeader}>
              <Badge variant={getRegistrationStatusConfig(myRegistration.status, myRegistration.isWaitlisted).variant}>
                {getRegistrationStatusConfig(myRegistration.status, myRegistration.isWaitlisted).label}
              </Badge>
              <Text style={styles.tapToViewText}>Tap to view details</Text>
            </View>

            {myRegistration.position && (
              <View style={styles.registrationRow}>
                <Text style={styles.registrationLabel}>Position:</Text>
                <Text style={styles.registrationValue}>{myRegistration.position}</Text>
              </View>
            )}

            <View style={styles.registrationRow}>
              <Text style={styles.registrationLabel}>Team:</Text>
              <Text style={styles.registrationValue}>
                {myRegistration.assignedTeamName || 'Not assigned yet'}
              </Text>
            </View>

            {tournament.entryFee > 0 && (
              <View style={styles.registrationRow}>
                <Text style={styles.registrationLabel}>Payment:</Text>
                <Badge
                  variant={
                    myRegistration.paymentStatus === 'Verified' ? 'green' :
                    myRegistration.paymentStatus === 'MarkedPaid' ? 'warning' :
                    'error'
                  }
                >
                  {myRegistration.paymentStatus === 'Verified' ? 'Verified' :
                   myRegistration.paymentStatus === 'MarkedPaid' ? 'Pending' :
                   'Required'}
                </Badge>
              </View>
            )}
          </TouchableOpacity>
        </View>
      )}

      {/* Bottom spacing */}
      <View style={styles.bottomSpacer} />
    </ScrollView>
  );
}

// Tab Button Component
interface TabButtonProps {
  label: string;
  isSelected: boolean;
  onPress: () => void;
}

function TabButton({ label, isSelected, onPress }: TabButtonProps) {
  return (
    <TouchableOpacity
      style={[styles.tabButton, isSelected && styles.tabButtonSelected]}
      onPress={onPress}
      activeOpacity={0.7}
    >
      <Text style={[styles.tabButtonText, isSelected && styles.tabButtonTextSelected]}>
        {label}
      </Text>
    </TouchableOpacity>
  );
}

// Tournament Segmented Control Component
interface TournamentTabControlProps {
  selectedTab: TabKey;
  onTabChange: (tab: TabKey) => void;
}

function TournamentTabControl({ selectedTab, onTabChange }: TournamentTabControlProps) {
  const tabs: { key: TabKey; label: string }[] = [
    { key: 'info', label: 'Info' },
    { key: 'bracket', label: 'Bracket' },
    { key: 'teams', label: 'Teams' },
  ];

  return (
    <View style={styles.tabControlContainer}>
      <View style={styles.tabsContainer}>
        {tabs.map((tab) => (
          <TabButton
            key={tab.key}
            label={tab.label}
            isSelected={selectedTab === tab.key}
            onPress={() => onTabChange(tab.key)}
          />
        ))}
      </View>
    </View>
  );
}


// Main Screen Component
export default function TournamentDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<TabKey>('info');
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [showStatusSheet, setShowStatusSheet] = useState(false);

  const { isAuthenticated, user } = useAuthStore();

  const {
    currentTournament,
    myRegistration,
    isLoading,
    error,
    fetchTournamentById,
    fetchMyRegistration,
    withdrawFromTournament,
    markPayment,
    clearTournament,
    clearError,
    clearRegistrations,
  } = useTournamentStore();

  useFocusEffect(
    useCallback(() => {
      if (id) {
        fetchTournamentById(id);
        // Fetch user's registration if authenticated
        if (isAuthenticated) {
          fetchMyRegistration(id);
        }
      }
      return () => {
        clearTournament();
        clearError();
        clearRegistrations();
      };
    }, [id, isAuthenticated])
  );

  const handleRefresh = async () => {
    if (!id) return;
    setIsRefreshing(true);
    try {
      await fetchTournamentById(id);
      if (isAuthenticated) {
        await fetchMyRegistration(id);
      }
    } finally {
      setIsRefreshing(false);
    }
  };

  const handleWithdraw = async () => {
    if (!id) return;
    const success = await withdrawFromTournament(id);
    if (success) {
      Alert.alert('Success', 'You have withdrawn from the tournament.');
      // Refresh data
      await handleRefresh();
    }
  };

  const handleMarkPayment = async () => {
    if (!id) return;

    Alert.alert(
      'Confirm Payment',
      'Have you completed payment to the tournament organizer? They will verify receipt.',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: "Yes, I've Paid",
          onPress: async () => {
            const success = await markPayment(id);
            if (success) {
              Alert.alert('Payment Marked', 'The organizer will verify your payment.');
              // Refresh registration
              await fetchMyRegistration(id);
            }
          },
        },
      ]
    );
  };

  const handleEdit = () => {
    // Future: Navigate to edit registration screen
    // For now, just close the sheet
    setShowStatusSheet(false);
  };

  const handleRegisterNow = () => {
    if (!id) return;
    router.push(`/tournaments/${id}/register`);
  };

  const handleTabChange = (tab: TabKey) => {
    if (tab === 'bracket') {
      router.push(`/tournaments/${id}/bracket`);
    } else if (tab === 'teams') {
      router.push(`/tournaments/${id}/teams`);
    } else {
      setActiveTab(tab);
    }
  };

  const getOrgAbbreviation = (name: string | undefined) => {
    if (!name) return 'Tournament';
    const words = name.split(' ');
    if (words.length > 1) {
      return words
        .slice(0, 3)
        .map((w) => w[0])
        .join('')
        .toUpperCase();
    }
    return name.slice(0, 3).toUpperCase();
  };

  const renderContent = () => {
    if (!currentTournament) return null;

    // Only Info tab renders inline; Bracket and Teams navigate to separate screens
    if (activeTab === 'info') {
      return (
        <InfoTab
          tournament={currentTournament}
          isRefreshing={isRefreshing}
          onRefresh={handleRefresh}
          myRegistration={myRegistration}
          onShowStatusSheet={() => setShowStatusSheet(true)}
        />
      );
    }

    return null;
  };

  const headerTitle = currentTournament
    ? getOrgAbbreviation(currentTournament.organizationName)
    : 'Tournament';

  const canManage = currentTournament?.canManage || false;
  const isRegistered = myRegistration && myRegistration.status !== 'Cancelled';
  const canRegister = currentTournament?.status === 'Open' && isAuthenticated && !isRegistered;

  return (
    <View style={styles.container}>
      <Stack.Screen
        options={{
          title: headerTitle,
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
          headerBackTitle: 'Back',
          headerRight: canManage
            ? () => (
                <TouchableOpacity
                  onPress={() => router.push(`/tournaments/edit?id=${currentTournament?.id}`)}
                  style={styles.headerButton}
                >
                  <Text style={styles.headerButtonText}>Edit</Text>
                </TouchableOpacity>
              )
            : isRegistered
            ? () => (
                <TouchableOpacity
                  onPress={() => setShowStatusSheet(true)}
                  style={styles.headerButton}
                >
                  <Badge variant="green">Registered</Badge>
                </TouchableOpacity>
              )
            : undefined,
        }}
      />

      {isLoading && !currentTournament ? (
        <View style={styles.centered}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
          <Text style={styles.loadingText}>Loading tournament...</Text>
        </View>
      ) : error ? (
        <View style={styles.centered}>
          <Text style={styles.errorText}>{error}</Text>
          <TouchableOpacity style={styles.retryButton} onPress={handleRefresh}>
            <Text style={styles.retryButtonText}>Retry</Text>
          </TouchableOpacity>
        </View>
      ) : currentTournament ? (
        <View style={styles.content}>
          {/* Tournament Header */}
          <View style={styles.header}>
            <View style={styles.headerTop}>
              <Text style={styles.tournamentName} numberOfLines={2}>
                {currentTournament.name}
              </Text>
              <TournamentStatusBadge status={currentTournament.status} />
            </View>
            {currentTournament.organizationName && (
              <Text style={styles.organizationName}>{currentTournament.organizationName}</Text>
            )}
          </View>

          {/* Tab Control */}
          <TournamentTabControl selectedTab={activeTab} onTabChange={handleTabChange} />

          {/* Tab Content */}
          <View style={styles.tabContent}>{renderContent()}</View>

          {/* Manage Footer (for organizers) */}
          {canManage && (
            <View style={styles.manageFooter}>
              <TouchableOpacity
                style={styles.manageButton}
                onPress={() => router.push(`/tournaments/${currentTournament.id}/manage`)}
                activeOpacity={0.8}
              >
                <Text style={styles.manageButtonText}>Manage Tournament</Text>
              </TouchableOpacity>
            </View>
          )}

          {/* Register Now Footer (for users who can register) */}
          {canRegister && (
            <View style={styles.registerFooter}>
              <TouchableOpacity
                style={styles.registerButton}
                onPress={handleRegisterNow}
                activeOpacity={0.8}
              >
                <Text style={styles.registerButtonText}>Register Now</Text>
              </TouchableOpacity>
            </View>
          )}
        </View>
      ) : null}

      {/* Registration Status Sheet */}
      {currentTournament && myRegistration && (
        <RegistrationStatusSheet
          visible={showStatusSheet}
          onClose={() => setShowStatusSheet(false)}
          registration={myRegistration}
          tournament={currentTournament}
          onEdit={handleEdit}
          onWithdraw={handleWithdraw}
          onMarkPayment={handleMarkPayment}
        />
      )}
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
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: spacing.lg,
    backgroundColor: colors.bg.darkest,
  },
  loadingText: {
    marginTop: spacing.sm,
    fontSize: 16,
    color: colors.text.muted,
  },
  errorText: {
    fontSize: 16,
    color: colors.status.error,
    textAlign: 'center',
    marginBottom: spacing.md,
  },
  retryButton: {
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
  },
  retryButtonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
  },
  headerButton: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
  },
  headerButtonText: {
    color: colors.primary.teal,
    fontSize: 16,
    fontWeight: '600',
  },

  // Tournament Header
  header: {
    backgroundColor: colors.bg.dark,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  headerTop: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: spacing.sm,
  },
  tournamentName: {
    flex: 1,
    fontSize: 20,
    fontWeight: '700',
    color: colors.text.primary,
  },
  organizationName: {
    marginTop: spacing.xs,
    fontSize: 14,
    color: colors.text.muted,
  },

  // Tab Control
  tabControlContainer: {
    backgroundColor: colors.bg.dark,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  tabsContainer: {
    flexDirection: 'row',
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.xs,
  },
  tabButton: {
    flex: 1,
    paddingVertical: spacing.sm,
    alignItems: 'center',
    borderRadius: radius.sm,
  },
  tabButtonSelected: {
    backgroundColor: colors.primary.teal,
  },
  tabButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.muted,
  },
  tabButtonTextSelected: {
    color: colors.bg.darkest,
  },

  // Tab Content
  tabContent: {
    flex: 1,
  },

  // Info Tab Styles
  scrollView: {
    flex: 1,
  },
  scrollContent: {
    padding: spacing.md,
  },
  infoSection: {
    marginBottom: spacing.lg,
  },
  sectionLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.muted,
    letterSpacing: 0.5,
    marginBottom: spacing.xs,
  },
  sectionValue: {
    fontSize: 16,
    color: colors.text.primary,
  },
  sectionValueMultiline: {
    fontSize: 14,
    color: colors.text.secondary,
    lineHeight: 20,
  },
  configRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: spacing.xs,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  configLabel: {
    fontSize: 14,
    color: colors.text.secondary,
  },
  configValue: {
    fontSize: 14,
    color: colors.text.primary,
    fontWeight: '500',
  },
  bottomSpacer: {
    height: spacing.xl,
  },

  // Manage Footer
  manageFooter: {
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },
  manageButton: {
    backgroundColor: colors.primary.teal,
    paddingVertical: spacing.md,
    borderRadius: radius.md,
    alignItems: 'center',
  },
  manageButtonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
  },

  // Register Footer
  registerFooter: {
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },
  registerButton: {
    backgroundColor: colors.primary.teal,
    paddingVertical: spacing.md,
    borderRadius: radius.md,
    alignItems: 'center',
  },
  registerButtonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
  },

  // Registration Card Styles
  registrationCard: {
    backgroundColor: colors.bg.elevated,
    padding: spacing.md,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  registrationHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  tapToViewText: {
    fontSize: 12,
    color: colors.text.muted,
  },
  registrationRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: spacing.xs,
    borderTopWidth: 1,
    borderTopColor: colors.border.muted,
    marginTop: spacing.xs,
  },
  registrationLabel: {
    fontSize: 14,
    color: colors.text.secondary,
  },
  registrationValue: {
    fontSize: 14,
    color: colors.text.primary,
    fontWeight: '500',
  },
});
