import { View, Text, StyleSheet, ScrollView, TouchableOpacity, RefreshControl, ActivityIndicator } from 'react-native';
import { useState, useEffect, useCallback, useMemo } from 'react';
import { useRouter } from 'expo-router';
import { useEventStore } from '../../stores/eventStore';
import { useAuthStore } from '../../stores/authStore';
import type { EventDto } from '@bhmhockey/shared';

// Helper to format date/time
function formatDateTime(dateString: string): string {
  const date = new Date(dateString);
  return date.toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}

// Helper to format payment status display
function getPaymentStatusText(status?: string): { text: string; color: string } {
  switch (status) {
    case 'Verified':
      return { text: 'Paid', color: '#22c55e' };
    case 'MarkedPaid':
      return { text: 'Awaiting Verification', color: '#f59e0b' };
    case 'Pending':
      return { text: 'Unpaid', color: '#ef4444' };
    default:
      return { text: 'Free', color: '#666' };
  }
}

// Component for individual game card
function GameCard({
  event,
  variant,
  onPress
}: {
  event: EventDto;
  variant: 'available' | 'registered' | 'organizing';
  onPress: () => void;
}) {
  const spotsLeft = event.maxPlayers - event.registeredCount;
  const paymentStatus = getPaymentStatusText(event.myPaymentStatus);

  return (
    <TouchableOpacity style={styles.gameCard} onPress={onPress}>
      <View style={styles.gameCardHeader}>
        <Text style={styles.gameName} numberOfLines={1}>{event.name}</Text>
        {event.cost > 0 && (
          <Text style={styles.gamePrice}>${event.cost}</Text>
        )}
      </View>

      <Text style={styles.gameDateTime}>{formatDateTime(event.eventDate)}</Text>

      {event.venue && (
        <Text style={styles.gameVenue} numberOfLines={1}>{event.venue}</Text>
      )}

      <View style={styles.gameCardFooter}>
        {variant === 'available' && (
          <View style={styles.availableFooterLeft}>
            <Text style={styles.gameOrg} numberOfLines={1}>
              {event.organizationName || 'Open Game'}
            </Text>
            {spotsLeft <= 2 && spotsLeft > 0 && (
              <Text style={styles.lowSpotsWarning}>
                Only {spotsLeft} {spotsLeft === 1 ? 'spot' : 'spots'} left!
              </Text>
            )}
          </View>
        )}

        {variant === 'registered' && (
          <>
            {event.cost > 0 ? (
              <Text style={[styles.paymentStatus, { color: paymentStatus.color }]}>
                {paymentStatus.text}
              </Text>
            ) : (
              <Text style={styles.gameOrg}>{event.organizationName || 'Open Game'}</Text>
            )}
          </>
        )}

        {variant === 'organizing' && (
          <>
            <Text style={styles.gameSpots}>
              {spotsLeft} spots left
            </Text>
            {event.cost > 0 ? (
              <Text style={styles.paidStatus}>
                <Text style={styles.paidCount}>{event.registeredCount - (event.unpaidCount ?? 0)} paid</Text>
                {event.unpaidCount !== undefined && event.unpaidCount > 0 && (
                  <Text style={styles.unpaidCount}> · {event.unpaidCount} unpaid</Text>
                )}
              </Text>
            ) : (
              <Text style={styles.gameSpots}>
                {event.registeredCount}/{event.maxPlayers} players
              </Text>
            )}
          </>
        )}
      </View>
    </TouchableOpacity>
  );
}

// Section header component
function SectionHeader({ title, count }: { title: string; count: number }) {
  return (
    <View style={styles.sectionHeader}>
      <Text style={styles.sectionTitle}>{title}</Text>
      <Text style={styles.sectionCount}>{count}</Text>
    </View>
  );
}

// Empty state component
function EmptyState({ message }: { message: string }) {
  return (
    <View style={styles.emptyState}>
      <Text style={styles.emptyStateText}>{message}</Text>
    </View>
  );
}

export default function HomeScreen() {
  const router = useRouter();
  const user = useAuthStore(state => state.user);
  const { events, myRegistrations, isLoading, fetchEvents, fetchMyRegistrations } = useEventStore();
  const [refreshing, setRefreshing] = useState(false);

  // Load data on mount
  useEffect(() => {
    if (user) {
      fetchEvents();
      fetchMyRegistrations();
    }
  }, [user]);

  // Pull to refresh
  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    await Promise.all([fetchEvents(), fetchMyRegistrations()]);
    setRefreshing(false);
  }, [fetchEvents, fetchMyRegistrations]);

  // Derived data
  const availableGames = useMemo(() =>
    events.filter(e =>
      !e.isRegistered &&
      e.registeredCount < e.maxPlayers &&
      e.status === 'Published'
    ),
    [events]
  );

  const myOrganizedGames = useMemo(() =>
    events.filter(e => e.isCreator),
    [events]
  );

  const handleEventPress = (eventId: string) => {
    router.push(`/events/${eventId}`);
  };

  // Show loading on initial load
  if (isLoading && events.length === 0) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#003366" />
        <Text style={styles.loadingText}>Loading games...</Text>
      </View>
    );
  }

  // If not logged in
  if (!user) {
    return (
      <View style={styles.loadingContainer}>
        <Text style={styles.loadingText}>Please log in to see games</Text>
      </View>
    );
  }

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
      }
    >
      <View style={styles.header}>
        <Text style={styles.greeting}>Hey {user.firstName}!</Text>
      </View>

      {/* Section 1: Upcoming Available Games */}
      <View style={styles.section}>
        <SectionHeader title="Upcoming Available Games" count={availableGames.length} />
        {availableGames.length === 0 ? (
          <EmptyState message="No games available right now" />
        ) : (
          <>
            {availableGames.slice(0, 2).map(event => (
              <GameCard
                key={event.id}
                event={event}
                variant="available"
                onPress={() => handleEventPress(event.id)}
              />
            ))}
            <TouchableOpacity
              style={styles.moreGamesButton}
              onPress={() => router.push('/events')}
            >
              <Text style={styles.moreGamesText}>More Games</Text>
            </TouchableOpacity>
          </>
        )}
      </View>

      {/* Section 2: My Registered Games */}
      <View style={styles.section}>
        <SectionHeader title="My Registered Games" count={myRegistrations.length} />
        {myRegistrations.length === 0 ? (
          <EmptyState message="You haven't registered for any games yet" />
        ) : (
          myRegistrations.map(event => (
            <GameCard
              key={event.id}
              event={event}
              variant="registered"
              onPress={() => handleEventPress(event.id)}
            />
          ))
        )}
      </View>

      {/* Section 3: Games I'm Organizing (only show if user has organized games) */}
      {myOrganizedGames.length > 0 && (
        <View style={styles.section}>
          <SectionHeader title="Games I'm Organizing" count={myOrganizedGames.length} />
          {myOrganizedGames.map(event => (
            <GameCard
              key={event.id}
              event={event}
              variant="organizing"
              onPress={() => handleEventPress(event.id)}
            />
          ))}
        </View>
      )}

      {/* Bottom padding */}
      <View style={{ height: 40 }} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#f5f5f5',
  },
  loadingText: {
    marginTop: 12,
    fontSize: 16,
    color: '#666',
  },
  header: {
    paddingHorizontal: 16,
    paddingTop: 16,
    paddingBottom: 8,
  },
  greeting: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#003366',
  },
  section: {
    marginTop: 16,
    paddingHorizontal: 16,
  },
  sectionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 12,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
  },
  sectionCount: {
    marginLeft: 8,
    fontSize: 14,
    color: '#666',
    backgroundColor: '#e5e5e5',
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 10,
  },
  gameCard: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.1,
    shadowRadius: 2,
    elevation: 2,
  },
  gameCardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 4,
  },
  gameName: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    flex: 1,
  },
  gamePrice: {
    fontSize: 16,
    fontWeight: '600',
    color: '#003366',
    marginLeft: 8,
  },
  gameDateTime: {
    fontSize: 14,
    color: '#666',
    marginBottom: 4,
  },
  gameVenue: {
    fontSize: 14,
    color: '#888',
    marginBottom: 8,
  },
  gameCardFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: 4,
  },
  availableFooterLeft: {
    flex: 1,
    gap: 2,
  },
  gameOrg: {
    fontSize: 13,
    color: '#666',
  },
  lowSpotsWarning: {
    fontSize: 13,
    color: '#ef4444',
    fontWeight: '600',
  },
  gameSpots: {
    fontSize: 13,
    color: '#003366',
    fontWeight: '500',
  },
  paymentStatus: {
    fontSize: 13,
    fontWeight: '600',
  },
  paidStatus: {
    fontSize: 13,
  },
  paidCount: {
    color: '#22c55e',
    fontWeight: '500',
  },
  unpaidCount: {
    color: '#ef4444',
    fontWeight: '500',
  },
  emptyState: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 24,
    alignItems: 'center',
  },
  emptyStateText: {
    fontSize: 14,
    color: '#888',
  },
  moreGamesButton: {
    backgroundColor: '#003366',
    borderRadius: 8,
    padding: 14,
    alignItems: 'center',
  },
  moreGamesText: {
    fontSize: 15,
    color: '#fff',
    fontWeight: '600',
  },
});
