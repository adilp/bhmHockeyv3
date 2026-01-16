import { useCallback, useMemo, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  ActivityIndicator,
  RefreshControl,
  TouchableOpacity,
  ScrollView,
  TextInput,
  Alert,
  ActionSheetIOS,
  Platform,
} from 'react-native';
import { useLocalSearchParams, Stack, useFocusEffect } from 'expo-router';
import { useTournamentStore } from '../../../../stores/tournamentStore';
import { Badge, EmptyState } from '../../../../components';
import { colors, spacing, radius } from '../../../../theme';
import type { TournamentRegistrationDto, PaymentStatus } from '@bhmhockey/shared';

type PaymentFilter = 'All' | PaymentStatus;
type PositionFilter = 'All' | 'Goalie' | 'Skater';
type AssignmentFilter = 'All' | 'Assigned' | 'Unassigned';

// Map payment status to badge variant
const getPaymentBadgeVariant = (status?: PaymentStatus) => {
  switch (status) {
    case 'Verified':
      return 'green';
    case 'MarkedPaid':
      return 'warning';
    case 'Pending':
    default:
      return 'default';
  }
};

export default function RegistrationsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [paymentFilter, setPaymentFilter] = useState<PaymentFilter>('All');
  const [positionFilter, setPositionFilter] = useState<PositionFilter>('All');
  const [assignmentFilter, setAssignmentFilter] = useState<AssignmentFilter>('All');

  const { registrations, fetchAllRegistrations, verifyPayment, isLoading } = useTournamentStore();

  useFocusEffect(
    useCallback(() => {
      if (id) fetchAllRegistrations(id);
    }, [id])
  );

  const handleRefresh = async () => {
    if (!id) return;
    setIsRefreshing(true);
    try {
      await fetchAllRegistrations(id);
    } finally {
      setIsRefreshing(false);
    }
  };

  // Filter registrations
  const filteredRegistrations = useMemo(() => {
    return registrations.filter((reg) => {
      // Search filter
      if (searchQuery) {
        const fullName = `${reg.user.firstName} ${reg.user.lastName}`.toLowerCase();
        if (!fullName.includes(searchQuery.toLowerCase())) {
          return false;
        }
      }

      // Payment status filter
      if (paymentFilter !== 'All') {
        if (reg.paymentStatus !== paymentFilter) {
          return false;
        }
      }

      // Position filter
      if (positionFilter !== 'All') {
        if (reg.position !== positionFilter) {
          return false;
        }
      }

      // Assignment filter
      if (assignmentFilter === 'Assigned' && !reg.assignedTeamId) {
        return false;
      }
      if (assignmentFilter === 'Unassigned' && reg.assignedTeamId) {
        return false;
      }

      return true;
    });
  }, [registrations, searchQuery, paymentFilter, positionFilter, assignmentFilter]);

  const handleRegistrationPress = (registration: TournamentRegistrationDto) => {
    if (!id) return;

    const currentStatus = registration.paymentStatus || 'Pending';
    const playerName = `${registration.user.firstName} ${registration.user.lastName}`;

    if (Platform.OS === 'ios') {
      ActionSheetIOS.showActionSheetWithOptions(
        {
          options: ['Cancel', 'Verify Payment', 'Reject Payment'],
          cancelButtonIndex: 0,
          destructiveButtonIndex: currentStatus === 'Verified' ? 2 : undefined,
          title: `${playerName} - ${currentStatus}`,
        },
        async (buttonIndex) => {
          if (buttonIndex === 0) return; // Cancel

          if (buttonIndex === 1) {
            // Verify Payment
            const success = await verifyPayment(id, registration.id, true);
            if (success) {
              Alert.alert('Success', `Payment verified for ${playerName}`);
            }
          } else if (buttonIndex === 2) {
            // Reject Payment
            Alert.alert(
              'Reject Payment',
              `Reset payment status to "Pending" for ${playerName}?`,
              [
                { text: 'Cancel', style: 'cancel' },
                {
                  text: 'Reject',
                  style: 'destructive',
                  onPress: async () => {
                    const success = await verifyPayment(id, registration.id, false);
                    if (success) {
                      Alert.alert('Done', `Payment rejected for ${playerName}`);
                    }
                  },
                },
              ]
            );
          }
        }
      );
    } else {
      // Android - use Alert.alert with buttons
      Alert.alert(`${playerName} - ${currentStatus}`, 'Choose an action:', [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Verify Payment',
          onPress: async () => {
            const success = await verifyPayment(id, registration.id, true);
            if (success) {
              Alert.alert('Success', `Payment verified for ${playerName}`);
            }
          },
        },
        {
          text: 'Reject Payment',
          style: 'destructive',
          onPress: async () => {
            const success = await verifyPayment(id, registration.id, false);
            if (success) {
              Alert.alert('Done', `Payment rejected for ${playerName}`);
            }
          },
        },
      ]);
    }
  };

  const renderRegistration = ({ item }: { item: TournamentRegistrationDto }) => (
    <TouchableOpacity style={styles.registrationRow} onPress={() => handleRegistrationPress(item)}>
      {/* Left section: Player info */}
      <View style={styles.registrationLeft}>
        <Text style={styles.playerName}>
          {item.user.firstName} {item.user.lastName}
        </Text>
        <View style={styles.metaRow}>
          {/* Position badge */}
          <Badge variant={item.position === 'Goalie' ? 'teal' : 'purple'} style={styles.positionBadge}>
            {item.position || 'Skater'}
          </Badge>

          {/* Team assignment */}
          <Text style={styles.teamText}>
            {item.assignedTeamName || 'Unassigned'}
          </Text>
        </View>
      </View>

      {/* Right section: Payment status */}
      <View style={styles.registrationRight}>
        <Badge variant={getPaymentBadgeVariant(item.paymentStatus)}>
          {item.paymentStatus || 'Pending'}
        </Badge>
      </View>
    </TouchableOpacity>
  );

  const ListHeaderComponent = () => (
    <View style={styles.headerContainer}>
      {/* Search bar */}
      <TextInput
        style={styles.searchInput}
        placeholder="Search by player name..."
        placeholderTextColor={colors.text.muted}
        value={searchQuery}
        onChangeText={setSearchQuery}
        autoCapitalize="none"
        autoCorrect={false}
      />

      {/* Filters */}
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.filtersContainer}
      >
        {/* Payment Status Filter */}
        <View style={styles.filterGroup}>
          <Text style={styles.filterLabel}>Payment:</Text>
          {(['All', 'Pending', 'MarkedPaid', 'Verified'] as PaymentFilter[]).map((filter) => (
            <TouchableOpacity
              key={filter}
              style={[
                styles.filterChip,
                paymentFilter === filter && styles.filterChipActive,
              ]}
              onPress={() => setPaymentFilter(filter)}
            >
              <Text
                style={[
                  styles.filterChipText,
                  paymentFilter === filter && styles.filterChipTextActive,
                ]}
              >
                {filter}
              </Text>
            </TouchableOpacity>
          ))}
        </View>

        {/* Position Filter */}
        <View style={styles.filterGroup}>
          <Text style={styles.filterLabel}>Position:</Text>
          {(['All', 'Goalie', 'Skater'] as PositionFilter[]).map((filter) => (
            <TouchableOpacity
              key={filter}
              style={[
                styles.filterChip,
                positionFilter === filter && styles.filterChipActive,
              ]}
              onPress={() => setPositionFilter(filter)}
            >
              <Text
                style={[
                  styles.filterChipText,
                  positionFilter === filter && styles.filterChipTextActive,
                ]}
              >
                {filter}
              </Text>
            </TouchableOpacity>
          ))}
        </View>

        {/* Assignment Filter */}
        <View style={styles.filterGroup}>
          <Text style={styles.filterLabel}>Team:</Text>
          {(['All', 'Assigned', 'Unassigned'] as AssignmentFilter[]).map((filter) => (
            <TouchableOpacity
              key={filter}
              style={[
                styles.filterChip,
                assignmentFilter === filter && styles.filterChipActive,
              ]}
              onPress={() => setAssignmentFilter(filter)}
            >
              <Text
                style={[
                  styles.filterChipText,
                  assignmentFilter === filter && styles.filterChipTextActive,
                ]}
              >
                {filter}
              </Text>
            </TouchableOpacity>
          ))}
        </View>
      </ScrollView>

      {/* Count */}
      <Text style={styles.countText}>
        {filteredRegistrations.length} of {registrations.length} registrations
      </Text>
    </View>
  );

  const ListEmptyComponent = () => {
    const hasActiveFilters =
      searchQuery !== '' ||
      paymentFilter !== 'All' ||
      positionFilter !== 'All' ||
      assignmentFilter !== 'All';

    return (
      <View style={styles.emptyContainer}>
        <EmptyState
          title={hasActiveFilters ? 'No Matches' : 'No Registrations Yet'}
          message={
            hasActiveFilters
              ? 'No registrations match your filters. Try adjusting them.'
              : 'No players have registered for this tournament yet.'
          }
        />
      </View>
    );
  };

  const headerTitle = `Registrations (${registrations.length})`;

  // Show loading spinner only on initial load (not during refresh)
  if (isLoading && registrations.length === 0) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Registrations',
            headerBackTitle: 'Back',
            headerStyle: { backgroundColor: colors.bg.dark },
            headerTintColor: colors.text.primary,
          }}
        />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
          <Text style={styles.loadingText}>Loading registrations...</Text>
        </View>
      </>
    );
  }

  return (
    <View style={styles.container}>
      <Stack.Screen
        options={{
          title: headerTitle,
          headerBackTitle: 'Back',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
        }}
      />

      <FlatList
        data={filteredRegistrations}
        keyExtractor={(item) => item.id}
        renderItem={renderRegistration}
        ListHeaderComponent={ListHeaderComponent}
        ListEmptyComponent={ListEmptyComponent}
        contentContainerStyle={[
          styles.listContent,
          filteredRegistrations.length === 0 && styles.emptyListContent,
        ]}
        refreshControl={
          <RefreshControl
            refreshing={isRefreshing}
            onRefresh={handleRefresh}
            tintColor={colors.primary.teal}
            colors={[colors.primary.teal]}
          />
        }
        ItemSeparatorComponent={() => <View style={styles.separator} />}
      />
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
    backgroundColor: colors.bg.darkest,
  },
  loadingText: {
    marginTop: spacing.sm,
    fontSize: 16,
    color: colors.text.muted,
  },
  listContent: {
    padding: spacing.md,
  },
  emptyListContent: {
    flex: 1,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingTop: spacing.xl,
  },

  // Header
  headerContainer: {
    marginBottom: spacing.md,
  },
  searchInput: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    fontSize: 15,
    color: colors.text.primary,
    borderWidth: 1,
    borderColor: colors.border.default,
    marginBottom: spacing.md,
  },
  filtersContainer: {
    paddingVertical: spacing.sm,
    gap: spacing.lg,
  },
  filterGroup: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.xs,
  },
  filterLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.muted,
    marginRight: spacing.xs,
  },
  filterChip: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: radius.sm,
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  filterChipActive: {
    backgroundColor: colors.primary.teal,
    borderColor: colors.primary.teal,
  },
  filterChipText: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.secondary,
  },
  filterChipTextActive: {
    color: colors.bg.darkest,
  },
  countText: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.muted,
    marginTop: spacing.md,
    marginBottom: spacing.xs,
  },

  // Registration row
  registrationRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  registrationLeft: {
    flex: 1,
    marginRight: spacing.sm,
  },
  playerName: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: spacing.xs,
  },
  metaRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
  },
  positionBadge: {
    paddingHorizontal: spacing.sm,
    paddingVertical: 2,
  },
  teamText: {
    fontSize: 12,
    color: colors.text.muted,
  },
  registrationRight: {
    alignItems: 'flex-end',
  },
  separator: {
    height: spacing.sm,
  },
});
