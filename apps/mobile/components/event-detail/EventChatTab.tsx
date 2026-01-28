import { View, StyleSheet } from 'react-native';
import { EmptyState } from '../EmptyState';
import { colors } from '../../theme';

export function EventChatTab() {
  return (
    <View style={styles.container}>
      <EmptyState
        icon="chatbubble-outline"
        title="Chat Coming Soon"
        message="Group chat for event participants will be available in a future update."
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
    justifyContent: 'center',
  },
});
