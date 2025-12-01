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
import type { EventDto } from '@bhmhockey/shared';

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

  const formatTime = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
    });
  };

  const renderEvent = ({ item }: { item: EventDto }) => {
    const spotsLeft = item.maxPlayers - item.registeredCount;
    const isFull = spotsLeft <= 0;
    const isLowSpots = spotsLeft > 0 && spotsLeft <= 2;
    const isMyEvent = item.canManage;

    // Calculate paid count for organizer view
    const paidCount = item.registeredCount - (item.unpaidCount ?? 0);

    return (
      <TouchableOpacity
        style={[styles.card, isMyEvent && styles.myEventCard]}
        onPress={() => router.push(`/events/${item.id}`)}
      >
        {/* My Event Label */}
        {isMyEvent && (
          <View style={styles.myEventLabel}>
            <Text style={styles.myEventLabelText}>My Event</Text>
          </View>
        )}

        <View style={styles.cardHeader}>
          <View style={[styles.dateBox, isMyEvent && styles.myEventDateBox]}>
            <Text style={styles.dateDay}>
              {new Date(item.eventDate).getDate()}
            </Text>
            <Text style={styles.dateMonth}>
              {new Date(item.eventDate).toLocaleDateString('en-US', { month: 'short' })}
            </Text>
          </View>
          <View style={styles.eventInfo}>
            <Text style={styles.eventName}>{item.name}</Text>
            <Text style={styles.organizationName}>
              {item.organizationName || 'Pickup Game'}
            </Text>
            <View style={styles.eventMeta}>
              <Text style={styles.eventTime}>
                {formatTime(item.eventDate)} ({item.duration} min)
              </Text>
              {item.visibility === 'InviteOnly' && (
                <View style={styles.visibilityBadge}>
                  <Text style={styles.visibilityText}>Invite Only</Text>
                </View>
              )}
            </View>
          </View>
          {item.isRegistered && !isMyEvent && (
            <View style={styles.registeredBadge}>
              <Text style={styles.registeredBadgeText}>Going</Text>
            </View>
          )}
        </View>

        {item.venue && (
          <Text style={styles.venue}>{item.venue}</Text>
        )}

        <View style={styles.cardFooter}>
          {/* Left side - different content for my events vs others */}
          {isMyEvent ? (
            <View style={styles.organizerInfo}>
              <Text style={styles.organizerSpots}>
                {spotsLeft} spots left
              </Text>
              {item.cost > 0 ? (
                <Text style={styles.organizerPayment}>
                  <Text style={styles.paidCount}>{paidCount} paid</Text>
                  {(item.unpaidCount ?? 0) > 0 && (
                    <Text style={styles.unpaidCount}> · {item.unpaidCount} unpaid</Text>
                  )}
                </Text>
              ) : (
                <Text style={styles.organizerCount}>
                  {item.registeredCount} registered
                </Text>
              )}
            </View>
          ) : (
            <View style={styles.spotsContainer}>
              {isFull ? (
                <>
                  <View style={[styles.spotsDot, styles.spotsFull]} />
                  <Text style={[styles.spotsText, styles.spotsTextFull]}>Full</Text>
                </>
              ) : isLowSpots ? (
                <>
                  <View style={[styles.spotsDot, styles.spotsLow]} />
                  <Text style={[styles.spotsText, styles.spotsTextLow]}>
                    Only {spotsLeft} {spotsLeft === 1 ? 'spot' : 'spots'} left!
                  </Text>
                </>
              ) : (
                <View style={styles.spotsPlaceholder} />
              )}
            </View>
          )}

          <View style={styles.footerRight}>
            {item.cost > 0 && (
              <Text style={styles.cost}>${item.cost.toFixed(0)}</Text>
            )}
          </View>
        </View>
      </TouchableOpacity>
    );
  };

  if (isLoading && events.length === 0) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color="#003366" />
        <Text style={styles.loadingText}>Loading events...</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <View style={styles.headerTop}>
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
            onRefresh={() => fetchEvents()}
            colors={['#003366']}
          />
        }
        ListEmptyComponent={
          <View style={styles.emptyState}>
            <Text style={styles.emptyTitle}>No Upcoming Events</Text>
            <Text style={styles.emptySubtitle}>
              Join organizations in the Orgs tab to see their events
            </Text>
          </View>
        }
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 12,
    fontSize: 16,
    color: '#666',
  },
  header: {
    padding: 20,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  headerTop: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    color: '#003366',
    marginBottom: 4,
  },
  subtitle: {
    fontSize: 16,
    color: '#666',
  },
  createButton: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: '#003366',
    justifyContent: 'center',
    alignItems: 'center',
  },
  createButtonText: {
    color: '#fff',
    fontSize: 28,
    fontWeight: '400',
    marginTop: -2,
  },
  errorBanner: {
    backgroundColor: '#FFE5E5',
    padding: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#FF3B30',
  },
  errorText: {
    color: '#FF3B30',
    textAlign: 'center',
  },
  list: {
    padding: 16,
  },
  card: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  myEventCard: {
    borderWidth: 2,
    borderColor: '#003366',
    backgroundColor: '#f8fafc',
  },
  myEventLabel: {
    position: 'absolute',
    top: -1,
    right: 16,
    backgroundColor: '#003366',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderBottomLeftRadius: 6,
    borderBottomRightRadius: 6,
  },
  myEventLabelText: {
    color: '#fff',
    fontSize: 11,
    fontWeight: '600',
  },
  cardHeader: {
    flexDirection: 'row',
    marginBottom: 12,
  },
  dateBox: {
    width: 50,
    height: 50,
    backgroundColor: '#003366',
    borderRadius: 8,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 12,
  },
  myEventDateBox: {
    backgroundColor: '#003366',
  },
  dateDay: {
    color: '#fff',
    fontSize: 20,
    fontWeight: 'bold',
  },
  dateMonth: {
    color: '#fff',
    fontSize: 11,
    fontWeight: '500',
    textTransform: 'uppercase',
  },
  eventInfo: {
    flex: 1,
    justifyContent: 'center',
  },
  eventName: {
    fontSize: 17,
    fontWeight: '600',
    marginBottom: 2,
  },
  organizationName: {
    fontSize: 14,
    color: '#666',
    marginBottom: 2,
  },
  eventMeta: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  eventTime: {
    fontSize: 13,
    color: '#999',
  },
  visibilityBadge: {
    backgroundColor: '#FFF3CD',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  visibilityText: {
    fontSize: 10,
    color: '#856404',
    fontWeight: '600',
  },
  registeredBadge: {
    backgroundColor: '#4CAF50',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 12,
    alignSelf: 'flex-start',
  },
  registeredBadgeText: {
    color: '#fff',
    fontSize: 12,
    fontWeight: '600',
  },
  venue: {
    fontSize: 14,
    color: '#666',
    marginBottom: 12,
    paddingLeft: 62,
  },
  cardFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: '#f0f0f0',
  },
  spotsContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    minHeight: 20,
  },
  spotsPlaceholder: {
    height: 20,
  },
  spotsDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
    marginRight: 6,
  },
  spotsAvailable: {
    backgroundColor: '#4CAF50',
  },
  spotsFull: {
    backgroundColor: '#FF3B30',
  },
  spotsLow: {
    backgroundColor: '#ef4444',
  },
  spotsText: {
    fontSize: 14,
    fontWeight: '500',
  },
  spotsTextFull: {
    color: '#FF3B30',
  },
  spotsTextLow: {
    color: '#ef4444',
    fontWeight: '600',
  },
  organizerInfo: {
    flexDirection: 'column',
    gap: 2,
  },
  organizerSpots: {
    fontSize: 14,
    color: '#003366',
    fontWeight: '500',
  },
  organizerPayment: {
    fontSize: 13,
  },
  organizerCount: {
    fontSize: 13,
    color: '#666',
  },
  paidCount: {
    color: '#22c55e',
    fontWeight: '500',
  },
  unpaidCount: {
    color: '#ef4444',
    fontWeight: '500',
  },
  footerRight: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  cost: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
  },
  emptyState: {
    alignItems: 'center',
    paddingVertical: 40,
  },
  emptyTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
    marginBottom: 8,
  },
  emptySubtitle: {
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
    paddingHorizontal: 20,
  },
});
