import { View, Text, StyleSheet, ScrollView, TouchableOpacity, RefreshControl, ActivityIndicator } from 'react-native';
import { useState, useCallback, useMemo } from 'react';
import { useRouter, useFocusEffect } from 'expo-router';
import { useEventStore } from '../../stores/eventStore';
import { useAuthStore } from '../../stores/authStore';
import { EventCard, SectionHeader, EmptyState } from '../../components';
import { colors, spacing } from '../../theme';

export default function HomeScreen() {
  const router = useRouter();
  const user = useAuthStore(state => state.user);
  const { events, myRegistrations, isLoading, fetchEvents, fetchMyRegistrations } = useEventStore();
  const [refreshing, setRefreshing] = useState(false);

  // Refresh data when screen comes into focus
  useFocusEffect(
    useCallback(() => {
      if (user) {
        fetchEvents();
        fetchMyRegistrations();
      }
    }, [user])
  );

  // Pull to refresh
  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    await Promise.all([fetchEvents(), fetchMyRegistrations()]);
    setRefreshing(false);
  }, [fetchEvents, fetchMyRegistrations]);

  // Helper to sort events by date (earliest first)
  const sortByDate = (a: typeof events[0], b: typeof events[0]) =>
    new Date(a.eventDate).getTime() - new Date(b.eventDate).getTime();

  // Derived data - all sorted by event date
  const availableGames = useMemo(() =>
    events
      .filter(e => !e.isRegistered && !e.amIWaitlisted && e.status === 'Published')
      .sort(sortByDate),
    [events]
  );

  // Waitlisted events (from events list since they're not in myRegistrations)
  const myWaitlistedGames = useMemo(() =>
    events.filter(e => e.amIWaitlisted && e.status === 'Published').sort(sortByDate),
    [events]
  );

  // Combined: registered + waitlisted (filter out cancelled)
  const myUpcomingGames = useMemo(() =>
    [...myRegistrations.filter(e => e.status === 'Published'), ...myWaitlistedGames].sort(sortByDate),
    [myRegistrations, myWaitlistedGames]
  );

  const myOrganizedGames = useMemo(() =>
    events.filter(e => e.canManage && e.status === 'Published').sort(sortByDate),
    [events]
  );

  // Helper to determine EventCard variant
  const getEventVariant = (event: typeof events[0]) => {
    if (event.amIWaitlisted) return 'waitlisted' as const;
    return 'registered' as const;
  };

  const handleEventPress = (eventId: string) => {
    router.push(`/events/${eventId}`);
  };

  // Show loading on initial load
  if (isLoading && events.length === 0) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
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
        <RefreshControl
          refreshing={refreshing}
          onRefresh={onRefresh}
          tintColor={colors.primary.teal}
          colors={[colors.primary.teal]}
          progressBackgroundColor={colors.bg.dark}
        />
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
              <EventCard
                key={event.id}
                event={event}
                variant="available"
                onPress={() => handleEventPress(event.id)}
              />
            ))}
            {availableGames.length > 2 && (
              <TouchableOpacity
                style={styles.moreButton}
                onPress={() => router.push('/events')}
              >
                <Text style={styles.moreButtonText}>More Games</Text>
              </TouchableOpacity>
            )}
          </>
        )}
      </View>

      {/* Section 2: My Upcoming Games (registered + waitlisted) */}
      <View style={styles.section}>
        <SectionHeader title="My Upcoming Games" count={myUpcomingGames.length} />
        {myUpcomingGames.length === 0 ? (
          <EmptyState message="You haven't registered for any games yet" />
        ) : (
          myUpcomingGames.map(event => (
            <EventCard
              key={event.id}
              event={event}
              variant={getEventVariant(event)}
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
            <EventCard
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
  header: {
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.lg,
    paddingBottom: spacing.xs,
  },
  greeting: {
    fontSize: 28,
    fontWeight: '700',
    color: colors.text.primary,
    letterSpacing: -0.5,
  },
  section: {
    marginTop: spacing.lg,
    paddingHorizontal: spacing.lg,
  },
  moreButton: {
    backgroundColor: colors.primary.teal,
    borderRadius: 10,
    padding: 14,
    alignItems: 'center',
  },
  moreButtonText: {
    fontSize: 15,
    color: colors.bg.darkest,
    fontWeight: '700',
    letterSpacing: 0.3,
  },
});
