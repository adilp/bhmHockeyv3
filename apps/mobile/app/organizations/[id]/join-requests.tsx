import { useCallback, useMemo, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
} from 'react-native';
import { useLocalSearchParams, useRouter, Stack, useFocusEffect } from 'expo-router';
import { organizationService } from '@bhmhockey/api-client';
import type { OrganizationJoinRequest } from '@bhmhockey/shared';
import { useOrganizationStore } from '../../../stores/organizationStore';
import { Badge } from '../../../components';
import { colors, spacing, radius } from '../../../theme';

function formatRequestedAt(dateString: string): string {
  // Timestamps come back as UTC but may not carry the "Z" suffix
  const utc = dateString.endsWith('Z') ? dateString : `${dateString}Z`;
  const date = new Date(utc);
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}

export default function JoinRequestsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();

  const joinRequests = useOrganizationStore((state) => state.joinRequests);
  const fetchJoinRequests = useOrganizationStore((state) => state.fetchJoinRequests);
  const approveJoinRequest = useOrganizationStore((state) => state.approveJoinRequest);
  const denyJoinRequest = useOrganizationStore((state) => state.denyJoinRequest);

  const [isLoading, setIsLoading] = useState(true);
  const [processingUserId, setProcessingUserId] = useState<string | null>(null);

  useFocusEffect(
    useCallback(() => {
      loadData();
    }, [id])
  );

  const loadData = async () => {
    if (!id) return;

    setIsLoading(true);
    try {
      const org = await organizationService.getById(id);
      if (!org.isAdmin) {
        Alert.alert('Access Denied', 'Only organization admins can manage join requests');
        router.back();
        return;
      }
      // "All" so an admin can revisit (and reverse) past denials
      await fetchJoinRequests(id, 'All');
    } catch (error) {
      Alert.alert('Error', 'Failed to load join requests');
      router.back();
    } finally {
      setIsLoading(false);
    }
  };

  const { pending, denied } = useMemo(() => {
    return {
      pending: joinRequests.filter((r) => r.status === 'Pending'),
      denied: joinRequests.filter((r) => r.status === 'Denied'),
    };
  }, [joinRequests]);

  const showStoreErrorOr = (fallback: string) => {
    const message = useOrganizationStore.getState().error || fallback;
    Alert.alert('Error', message);
  };

  const handleApprove = async (request: OrganizationJoinRequest) => {
    if (!id || processingUserId) return;

    setProcessingUserId(request.userId);
    const success = await approveJoinRequest(id, request.userId);
    setProcessingUserId(null);

    if (!success) {
      showStoreErrorOr('Failed to approve join request');
    }
  };

  const handleDeny = (request: OrganizationJoinRequest) => {
    if (!id || processingUserId) return;

    Alert.alert(
      'Decline Request',
      `Decline ${request.firstName} ${request.lastName}? They will not be able to request again, but you can still approve them from this screen.`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Decline',
          style: 'destructive',
          onPress: async () => {
            setProcessingUserId(request.userId);
            const success = await denyJoinRequest(id, request.userId);
            setProcessingUserId(null);

            if (!success) {
              showStoreErrorOr('Failed to decline join request');
            }
          },
        },
      ]
    );
  };

  const renderRequest = (request: OrganizationJoinRequest) => {
    const isProcessing = processingUserId === request.userId;

    return (
      <View key={request.id} style={styles.requestRow}>
        <View style={styles.requestInfo}>
          <View style={styles.nameRow}>
            <Text style={styles.requesterName} allowFontScaling={false}>
              {request.firstName} {request.lastName}
            </Text>
            {request.status === 'Denied' && <Badge variant="error">Declined</Badge>}
          </View>
          <Text style={styles.requestedAt} allowFontScaling={false}>
            Asked {formatRequestedAt(request.requestedAt)}
          </Text>
        </View>

        {isProcessing ? (
          <ActivityIndicator size="small" color={colors.primary.teal} />
        ) : (
          <View style={styles.rowActions}>
            <TouchableOpacity
              style={styles.approveButton}
              onPress={() => handleApprove(request)}
              disabled={!!processingUserId}
            >
              <Text style={styles.approveButtonText} allowFontScaling={false}>
                Approve
              </Text>
            </TouchableOpacity>
            {request.status === 'Pending' && (
              <TouchableOpacity
                style={styles.denyButton}
                onPress={() => handleDeny(request)}
                disabled={!!processingUserId}
              >
                <Text style={styles.denyButtonText} allowFontScaling={false}>
                  Decline
                </Text>
              </TouchableOpacity>
            )}
          </View>
        )}
      </View>
    );
  };

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Join Requests',
          headerBackTitle: 'Back',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
        }}
      />

      {isLoading ? (
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
        </View>
      ) : (
        <ScrollView style={styles.container}>
          <View style={styles.explanationBox}>
            <Text style={styles.explanationText} allowFontScaling={false}>
              This organization is private, so people have to ask before they can join. Approving
              adds them as a member right away.
            </Text>
          </View>

          <Text style={styles.sectionLabel} allowFontScaling={false}>
            Pending {pending.length > 0 ? `(${pending.length})` : ''}
          </Text>
          {pending.length === 0 ? (
            <View style={styles.emptyBox}>
              <Text style={styles.emptyText} allowFontScaling={false}>
                No pending requests
              </Text>
            </View>
          ) : (
            <View style={styles.list}>{pending.map(renderRequest)}</View>
          )}

          {denied.length > 0 && (
            <>
              <Text style={styles.sectionLabel} allowFontScaling={false}>
                Declined ({denied.length})
              </Text>
              <Text style={styles.sectionHint} allowFontScaling={false}>
                Declined people can't ask again. Approve to let someone in after all.
              </Text>
              <View style={styles.list}>{denied.map(renderRequest)}</View>
            </>
          )}

          <View style={{ height: 40 }} />
        </ScrollView>
      )}
    </>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
    padding: spacing.md,
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.darkest,
  },
  explanationBox: {
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderRadius: radius.md,
    marginBottom: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  explanationText: {
    fontSize: 14,
    color: colors.text.secondary,
    lineHeight: 20,
  },
  sectionLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.xs,
    marginTop: spacing.md,
  },
  sectionHint: {
    fontSize: 13,
    color: colors.text.subtle,
    marginBottom: spacing.sm,
  },
  emptyBox: {
    alignItems: 'center',
    padding: spacing.lg,
  },
  emptyText: {
    fontSize: 15,
    color: colors.text.muted,
    textAlign: 'center',
  },
  list: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  requestRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border.muted,
  },
  requestInfo: {
    flex: 1,
  },
  nameRow: {
    flexDirection: 'row',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: spacing.sm,
  },
  requesterName: {
    fontSize: 15,
    fontWeight: '500',
    color: colors.text.primary,
  },
  requestedAt: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: 2,
  },
  rowActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
  },
  approveButton: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.primary.teal,
    backgroundColor: colors.subtle.teal,
  },
  approveButtonText: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.primary.teal,
  },
  denyButton: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.status.error,
    backgroundColor: 'transparent',
  },
  denyButtonText: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.status.error,
  },
});
