import { useEffect, useRef, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Linking,
  NativeSyntheticEvent,
  NativeScrollEvent,
} from 'react-native';
import type { OrganizationWaiver } from '@bhmhockey/shared';
import { getApiUrl } from '../config/api';
import { colors, spacing, radius } from '../theme';

// How close to the bottom (px) counts as "scrolled to the bottom"
const SCROLL_BOTTOM_THRESHOLD = 24;

interface WaiverAcceptanceModalProps {
  visible: boolean;
  organizationName: string;
  waiver: OrganizationWaiver;
  /** Called after the user taps Agree (caller performs the accept API call) */
  onAgree: () => void | Promise<void>;
  /**
   * Dismissible mode (registration flow): renders a Close button and allows
   * hardware-back dismissal. Omit for the blocking accept-or-leave gate.
   */
  onClose?: () => void;
  /** Blocking gate only: destructive "Leave Organization" secondary action */
  onLeaveOrganization?: () => void;
}

/**
 * Full-screen legal waiver acceptance modal. The Agree button stays disabled
 * until the user has scrolled the waiver text to the bottom (enabled
 * immediately when the text fits without scrolling).
 */
export function WaiverAcceptanceModal({
  visible,
  organizationName,
  waiver,
  onAgree,
  onClose,
  onLeaveOrganization,
}: WaiverAcceptanceModalProps) {
  const [hasScrolledToBottom, setHasScrolledToBottom] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const scrollViewHeightRef = useRef(0);
  const contentHeightRef = useRef(0);

  // A new waiver version (or reopening) requires reading again
  useEffect(() => {
    if (visible) {
      setHasScrolledToBottom(false);
      setIsProcessing(false);
    }
  }, [visible, waiver.id]);

  const maybeEnableWithoutScrolling = () => {
    // If the content fits in the viewport there is nothing to scroll - enable
    if (
      scrollViewHeightRef.current > 0 &&
      contentHeightRef.current > 0 &&
      contentHeightRef.current <= scrollViewHeightRef.current
    ) {
      setHasScrolledToBottom(true);
    }
  };

  const handleScroll = (event: NativeSyntheticEvent<NativeScrollEvent>) => {
    const { layoutMeasurement, contentOffset, contentSize } = event.nativeEvent;
    if (layoutMeasurement.height + contentOffset.y >= contentSize.height - SCROLL_BOTTOM_THRESHOLD) {
      setHasScrolledToBottom(true);
    }
  };

  const handleAgree = async () => {
    setIsProcessing(true);
    try {
      await onAgree();
    } finally {
      setIsProcessing(false);
    }
  };

  const handleViewPdf = () => {
    // Public endpoint - opens in the browser where it can be viewed/saved
    Linking.openURL(`${getApiUrl()}/organizations/${waiver.organizationId}/waiver/pdf`);
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      onRequestClose={onClose ?? (() => {})}
    >
      <View style={styles.container}>
        <View style={styles.header}>
          <Text style={styles.title} allowFontScaling={false}>
            Legal Waiver
          </Text>
          <Text style={styles.orgName} allowFontScaling={false}>
            {organizationName}
          </Text>
          <Text style={styles.versionLine} allowFontScaling={false}>
            Version {waiver.version} · {new Date(waiver.createdAt).toLocaleDateString('en-US', {
              year: 'numeric',
              month: 'long',
              day: 'numeric',
            })}
          </Text>
        </View>

        <ScrollView
          style={styles.textScroll}
          contentContainerStyle={styles.textScrollContent}
          onLayout={(event) => {
            scrollViewHeightRef.current = event.nativeEvent.layout.height;
            maybeEnableWithoutScrolling();
          }}
          onContentSizeChange={(_, height) => {
            contentHeightRef.current = height;
            maybeEnableWithoutScrolling();
          }}
          onScroll={handleScroll}
          scrollEventThrottle={100}
        >
          <Text style={styles.waiverText} allowFontScaling={false}>
            {waiver.text}
          </Text>
        </ScrollView>

        <View style={styles.footer}>
          <TouchableOpacity onPress={handleViewPdf} style={styles.pdfLink}>
            <Text style={styles.pdfLinkText} allowFontScaling={false}>
              View / Save as PDF
            </Text>
          </TouchableOpacity>

          {!hasScrolledToBottom && (
            <Text style={styles.scrollHint} allowFontScaling={false}>
              Scroll to the bottom to enable Agree
            </Text>
          )}

          <TouchableOpacity
            style={[styles.agreeButton, (!hasScrolledToBottom || isProcessing) && styles.agreeButtonDisabled]}
            onPress={handleAgree}
            disabled={!hasScrolledToBottom || isProcessing}
          >
            {isProcessing ? (
              <ActivityIndicator color={colors.bg.darkest} />
            ) : (
              <Text style={styles.agreeButtonText} allowFontScaling={false}>
                I Agree
              </Text>
            )}
          </TouchableOpacity>

          {onLeaveOrganization && (
            <TouchableOpacity
              style={styles.leaveButton}
              onPress={onLeaveOrganization}
              disabled={isProcessing}
            >
              <Text style={styles.leaveButtonText} allowFontScaling={false}>
                Leave Organization
              </Text>
            </TouchableOpacity>
          )}

          {onClose && (
            <TouchableOpacity style={styles.closeButton} onPress={onClose} disabled={isProcessing}>
              <Text style={styles.closeButtonText} allowFontScaling={false}>
                Not Now
              </Text>
            </TouchableOpacity>
          )}
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
    paddingTop: spacing.xl + spacing.lg,
  },
  header: {
    paddingHorizontal: spacing.lg,
    paddingBottom: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  title: {
    fontSize: 22,
    fontWeight: '700',
    color: colors.text.primary,
  },
  orgName: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.primary.teal,
    marginTop: spacing.xs,
  },
  versionLine: {
    fontSize: 13,
    color: colors.text.muted,
    marginTop: spacing.xs,
  },
  textScroll: {
    flex: 1,
  },
  textScrollContent: {
    padding: spacing.lg,
  },
  waiverText: {
    fontSize: 15,
    lineHeight: 24,
    color: colors.text.secondary,
  },
  footer: {
    padding: spacing.lg,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
    backgroundColor: colors.bg.dark,
  },
  pdfLink: {
    alignItems: 'center',
    paddingVertical: spacing.sm,
    marginBottom: spacing.sm,
  },
  pdfLinkText: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.primary.teal,
    textDecorationLine: 'underline',
  },
  scrollHint: {
    fontSize: 13,
    color: colors.text.muted,
    textAlign: 'center',
    marginBottom: spacing.sm,
  },
  agreeButton: {
    backgroundColor: colors.primary.teal,
    paddingVertical: spacing.md,
    borderRadius: radius.md,
    alignItems: 'center',
  },
  agreeButtonDisabled: {
    opacity: 0.4,
  },
  agreeButtonText: {
    color: colors.bg.darkest,
    fontSize: 17,
    fontWeight: '600',
  },
  leaveButton: {
    marginTop: spacing.md,
    paddingVertical: spacing.md,
    borderRadius: radius.md,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.status.error,
  },
  leaveButtonText: {
    color: colors.status.error,
    fontSize: 15,
    fontWeight: '600',
  },
  closeButton: {
    marginTop: spacing.md,
    paddingVertical: spacing.sm,
    alignItems: 'center',
  },
  closeButtonText: {
    color: colors.text.muted,
    fontSize: 15,
  },
});
