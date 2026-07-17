import { View, Text, StyleSheet, TouchableOpacity, Linking, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import type { EventDto } from '@bhmhockey/shared';
import { EmptyState } from '../EmptyState';
import { Badge } from '../Badge';
import { colors, spacing, radius } from '../../theme';

interface EventChatTabProps {
  event: EventDto;
}

export function EventChatTab({ event }: EventChatTabProps) {
  const router = useRouter();
  const link = event.groupMeLink;

  if (!link) {
    return (
      <View style={styles.container}>
        <EmptyState
          icon="chatbubble-outline"
          title="No Chat Link Yet"
          message={
            event.canManage
              ? 'Add a GroupMe link to this event, or set an org-wide link in your organization settings.'
              : "The organizer hasn't added a GroupMe chat link for this event yet."
          }
          actionLabel={event.canManage ? 'Add GroupMe Link' : undefined}
          onAction={event.canManage ? () => router.push(`/events/edit?id=${event.id}`) : undefined}
        />
      </View>
    );
  }

  const isOrgChat = event.groupMeLinkSource === 'organization';

  const handleOpenGroupMe = async () => {
    try {
      await Linking.openURL(link);
    } catch (error) {
      Alert.alert('Error', 'Could not open the GroupMe link.');
    }
  };

  return (
    <View style={styles.container}>
      <View style={styles.card}>
        <Ionicons name="chatbubbles" size={48} color={colors.primary.teal} style={styles.icon} />
        <Text style={styles.title} allowFontScaling={false}>
          GroupMe
        </Text>
        <Badge variant={isOrgChat ? 'purple' : 'teal'}>
          {isOrgChat ? 'Organization Chat' : 'Event Chat'}
        </Badge>
        <Text style={styles.message} allowFontScaling={false}>
          {isOrgChat
            ? `This game uses ${event.organizationName || 'the organization'}'s org-wide GroupMe chat.`
            : 'This game has its own GroupMe chat.'}
        </Text>
        <TouchableOpacity style={styles.openButton} onPress={handleOpenGroupMe}>
          <Ionicons name="open-outline" size={18} color={colors.bg.darkest} />
          <Text style={styles.openButtonText} allowFontScaling={false}>
            Open GroupMe
          </Text>
        </TouchableOpacity>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
    justifyContent: 'center',
    padding: spacing.md,
  },
  card: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.xl,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  icon: {
    marginBottom: spacing.md,
  },
  title: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: spacing.sm,
  },
  message: {
    fontSize: 14,
    color: colors.text.secondary,
    textAlign: 'center',
    marginTop: spacing.md,
  },
  openButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.xs,
    marginTop: spacing.lg,
    backgroundColor: colors.primary.teal,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
    borderRadius: radius.md,
  },
  openButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.bg.darkest,
  },
});
