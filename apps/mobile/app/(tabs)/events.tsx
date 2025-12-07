import { useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  ActivityIndicator,
  RefreshControl,
} from 'react-native';
import { useRouter } from 'expo-router';
import { useEventStore } from '../../stores/eventStore';
import { useAuthStore } from '../../stores/authStore';
import { EventCard, EmptyState } from '../../components';
import { colors, spacing, radius } from '../../theme';
import type { EventDto } from '@bhmhockey/shared';
import type { EventCardVariant } from '../../components';

// Determine card variant based on user's relationship to event
function getEventVariant(event: EventDto): EventCardVariant {
  if (event.canManage) return 'organizing';
  if (event.isRegistered) return 'registered';
  return 'available';
}

export default function EventsScreen() {
  const router = useRouter();
  const { isAuthenticated } = useAuthStore();
  const {
    events,
    isLoading,
    error,
    fetchEvents,
  } = useEventStore();

  useEffect(() => {
    fetchEvents();
  }, []);

  const handleEventPress = (eventId: string) => {
    router.push(`/events/${eventId}`);
  };

  const renderEvent = ({ item }: { item: EventDto }) => (
    <EventCard
      event={item}
      variant={getEventVariant(item)}
      onPress={() => handleEventPress(item.id)}
    />
  );

  if (isLoading && events.length === 0) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
        <Text style={styles.loadingText}>Loading events...</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <View>
            <Text style={styles.title}>Events</Text>
            <Text style={styles.subtitle}>Upcoming hockey events</Text>
          </View>
          {isAuthenticated && (
            <TouchableOpacity
              style={styles.createButton}
              onPress={() => router.push('/events/create')}
            >
              <Text style={styles.createButtonText}>+</Text>
            </TouchableOpacity>
          )}
        </View>
      </View>

      {/* Error banner */}
      {error && (
        <View style={styles.errorBanner}>
          <Text style={styles.errorText}>{error}</Text>
        </View>
      )}

      <FlatList
        data={events}
        renderItem={renderEvent}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.list}
        refreshControl={
          <RefreshControl
            refreshing={isLoading}
            onRefresh={fetchEvents}
            tintColor={colors.primary.teal}
            colors={[colors.primary.teal]}
            progressBackgroundColor={colors.bg.dark}
          />
        }
        ListEmptyComponent={
          <EmptyState
            icon="📅"
            title="No Upcoming Events"
            message="Join organizations to see their events, or create your own!"
            actionLabel={isAuthenticated ? "Create Event" : undefined}
            onAction={isAuthenticated ? () => router.push('/events/create') : undefined}
          />
        }
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
  header: {
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.lg,
    paddingBottom: spacing.md,
    backgroundColor: colors.bg.darkest,
  },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  title: {
    fontSize: 28,
    fontWeight: '700',
    color: colors.text.primary,
    letterSpacing: -0.5,
  },
  subtitle: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: 2,
  },
  createButton: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: colors.primary.teal,
    justifyContent: 'center',
    alignItems: 'center',
  },
  createButtonText: {
    color: colors.bg.darkest,
    fontSize: 28,
    fontWeight: '400',
    marginTop: -2,
  },
  errorBanner: {
    backgroundColor: colors.status.errorSubtle,
    padding: spacing.sm,
    marginHorizontal: spacing.lg,
    borderRadius: radius.md,
  },
  errorText: {
    color: colors.status.error,
    textAlign: 'center',
    fontSize: 14,
  },
  list: {
    padding: spacing.lg,
  },
});
