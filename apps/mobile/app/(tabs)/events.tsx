import { useEffect, useState, useMemo } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  ActivityIndicator,
  RefreshControl,
  ScrollView,
  Alert,
} from 'react-native';
import { useRouter } from 'expo-router';
import { useEventStore } from '../../stores/eventStore';
import { useAuthStore } from '../../stores/authStore';
import type { UserRole } from '@bhmhockey/shared';
import { EventCard, EmptyState } from '../../components';
import { colors, spacing, radius } from '../../theme';
import type { EventDto } from '@bhmhockey/shared';
import type { EventCardVariant } from '../../components';

// Filter options
type FilterOption = 'all' | 'available' | 'registered' | 'organizing' | 'waitlisted';

const FILTER_OPTIONS: { key: FilterOption; label: string }[] = [
  { key: 'all', label: 'All' },
  { key: 'available', label: 'Available' },
  { key: 'registered', label: 'Registered' },
  { key: 'waitlisted', label: 'Waitlisted' },
  { key: 'organizing', label: 'Organizing' },
];

// Determine card variant based on user's relationship to event
function getEventVariant(event: EventDto): EventCardVariant {
  if (event.canManage) return 'organizing';
  if (event.isRegistered) return 'registered';
  if (event.amIWaitlisted) return 'waitlisted';
  return 'available';
}

const canCreateContent = (role?: UserRole): boolean => {
  return role === 'Organizer' || role === 'Admin';
};

const showOrganizerAccessDialog = () => {
  Alert.alert(
    'Organizer Access Required',
    'Creating events and organizations is limited to approved organizers. Please contact Adil Patel to request organizer access.',
    [{ text: 'OK' }]
  );
};

export default function EventsScreen() {
  const router = useRouter();
  const { isAuthenticated, user } = useAuthStore();
  const {
    events,
    isLoading,
    error,
    fetchEvents,
  } = useEventStore();

  const [activeFilter, setActiveFilter] = useState<FilterOption>('all');

  useEffect(() => {
    fetchEvents();
  }, []);

  // Filter events based on selected filter and sort by date
  const filteredEvents = useMemo(() => {
    let filtered: EventDto[];
    switch (activeFilter) {
      case 'available':
        filtered = events.filter(e => !e.isRegistered && !e.canManage && !e.amIWaitlisted);
        break;
      case 'registered':
        filtered = events.filter(e => e.isRegistered);
        break;
      case 'organizing':
        filtered = events.filter(e => e.canManage);
        break;
      case 'waitlisted':
        filtered = events.filter(e => e.amIWaitlisted);
        break;
      default:
        filtered = [...events];
    }
    // Sort by event date ascending (earliest first)
    return filtered.sort((a, b) => new Date(a.eventDate).getTime() - new Date(b.eventDate).getTime());
  }, [events, activeFilter]);

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
              onPress={() => {
                if (canCreateContent(user?.role)) {
                  router.push('/events/create');
                } else {
                  showOrganizerAccessDialog();
                }
              }}
            >
              <Text style={styles.createButtonText}>+</Text>
            </TouchableOpacity>
          )}
        </View>
      </View>

      {/* Filter Pills */}
      <View style={styles.filterContainer}>
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={styles.filterContent}
        >
        {FILTER_OPTIONS.map((filter) => (
          <TouchableOpacity
            key={filter.key}
            style={[
              styles.filterPill,
              activeFilter === filter.key && styles.filterPillActive,
            ]}
            onPress={() => setActiveFilter(filter.key)}
          >
            <Text
              style={[
                styles.filterPillText,
                activeFilter === filter.key && styles.filterPillTextActive,
              ]}
            >
              {filter.label}
            </Text>
          </TouchableOpacity>
        ))}
        </ScrollView>
      </View>

      {/* Error banner */}
      {error && (
        <View style={styles.errorBanner}>
          <Text style={styles.errorText}>{error}</Text>
        </View>
      )}

      <FlatList
        data={filteredEvents}
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
          activeFilter === 'all' ? (
            <EmptyState
              icon="calendar-outline"
              title="No Upcoming Events"
              message="Join organizations to see their events, or create your own!"
              actionLabel={isAuthenticated ? "Create Event" : undefined}
              onAction={isAuthenticated ? () => {
                if (canCreateContent(user?.role)) {
                  router.push('/events/create');
                } else {
                  showOrganizerAccessDialog();
                }
              } : undefined}
            />
          ) : activeFilter === 'available' ? (
            <EmptyState
              icon="calendar-outline"
              title="No Available Events"
              message="All events are full or you're already signed up."
            />
          ) : activeFilter === 'registered' ? (
            <EmptyState
              icon="calendar-outline"
              title="No Registered Events"
              message="Events you've registered for will appear here."
            />
          ) : activeFilter === 'waitlisted' ? (
            <EmptyState
              icon="calendar-outline"
              title="Not on Any Waitlists"
              message="Events you're waitlisted for will appear here."
            />
          ) : (
            <EmptyState
              icon="calendar-outline"
              title="No Events to Manage"
              message={isAuthenticated ? "Events you create or organize will appear here." : "Log in to manage events."}
              actionLabel={isAuthenticated && canCreateContent(user?.role) ? "Create Event" : undefined}
              onAction={isAuthenticated && canCreateContent(user?.role) ? () => router.push('/events/create') : undefined}
            />
          )
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
  // Filter Pills styles (matching design-reference-rows.html)
  filterContainer: {
    backgroundColor: colors.bg.darkest,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
    flexGrow: 0,
    flexShrink: 0,
  },
  filterContent: {
    paddingHorizontal: spacing.md,
    paddingVertical: 12,
    flexDirection: 'row',
    alignItems: 'center',
  },
  filterPill: {
    paddingHorizontal: 16,
    paddingVertical: 10,
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.muted,
    borderRadius: 9999, // pill shape
    marginRight: spacing.sm,
  },
  filterPillActive: {
    backgroundColor: colors.primary.teal,
    borderColor: colors.primary.teal,
  },
  filterPillText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.secondary,
    lineHeight: 18,
  },
  filterPillTextActive: {
    color: colors.bg.darkest,
  },
});
