import { useEffect, useRef, useState } from 'react';
import {
  View,
  Text,
  TextInput,
  StyleSheet,
  Modal,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  KeyboardAvoidingView,
  Linking,
  NativeSyntheticEvent,
  NativeScrollEvent,
  Platform,
} from 'react-native';
import type { OrganizationWaiver, WaiverSignatureDetails } from '@bhmhockey/shared';
import { useAuthStore } from '../stores/authStore';
import { getApiUrl } from '../config/api';
import { parseWaiverSegments } from '../utils/waiverFormat';
import {
  emptyWaiverSignatureForm,
  todayMMDDYYYY,
  validateWaiverSignature,
  type WaiverSignatureFormValues,
} from '../utils/waiverSignature';
import { colors, spacing, radius } from '../theme';

// How close to the bottom (px) counts as "scrolled to the bottom"
const SCROLL_BOTTOM_THRESHOLD = 24;

// Matches the server-side cap on signature text fields
const SIGNATURE_FIELD_MAX_LENGTH = 200;

interface WaiverAcceptanceModalProps {
  visible: boolean;
  organizationName: string;
  waiver: OrganizationWaiver;
  /** Called after the user taps Agree (caller performs the accept API call) */
  onAgree: (signature: WaiverSignatureDetails) => void | Promise<void>;
  /**
   * Dismissible mode (registration flow): renders a Close button and allows
   * hardware-back dismissal. Omit for the blocking accept-or-leave gate.
   */
  onClose?: () => void;
  /** Blocking gate only: destructive "Leave Organization" secondary action */
  onLeaveOrganization?: () => void;
}

type FieldKey = keyof WaiverSignatureFormValues;

interface SignatureFieldProps {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  onBlur: () => void;
  error?: string;
  placeholder?: string;
  /** Renders the entered text in an italic "signature" style */
  signature?: boolean;
  isDate?: boolean;
}

// Signature dates are always today and are stamped by the server at
// acceptance time - shown read-only so they cannot be backdated
function ReadOnlyDateField({ label }: { label: string }) {
  return (
    <View style={styles.field}>
      <Text style={styles.fieldLabel} allowFontScaling={false}>{label}</Text>
      <View style={[styles.fieldInput, styles.readOnlyField]}>
        <Text style={styles.readOnlyFieldText} allowFontScaling={false}>
          {todayMMDDYYYY()}
        </Text>
      </View>
    </View>
  );
}

function SignatureField({
  label,
  value,
  onChangeText,
  onBlur,
  error,
  placeholder,
  signature,
  isDate,
}: SignatureFieldProps) {
  return (
    <View style={styles.field}>
      <Text style={styles.fieldLabel} allowFontScaling={false}>{label}</Text>
      <TextInput
        style={[
          styles.fieldInput,
          signature && styles.signatureInput,
          error != null && styles.fieldInputError,
        ]}
        value={value}
        onChangeText={onChangeText}
        onBlur={onBlur}
        placeholder={placeholder}
        placeholderTextColor={colors.text.muted}
        maxLength={isDate ? 10 : SIGNATURE_FIELD_MAX_LENGTH}
        autoCapitalize={isDate ? 'none' : 'words'}
        autoCorrect={false}
        allowFontScaling={false}
      />
      {error != null && (
        <Text style={styles.fieldError} allowFontScaling={false}>{error}</Text>
      )}
    </View>
  );
}

/**
 * Full-screen legal waiver acceptance modal. Scrolling the waiver text to the
 * bottom reveals the signature form (adult participant fields plus an optional
 * all-or-nothing Parent/Guardian section for minors); the Agree button stays
 * disabled until the text has been read AND the form validates (enabled
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
  const [form, setForm] = useState<WaiverSignatureFormValues>(emptyWaiverSignatureForm);
  const [touched, setTouched] = useState<Partial<Record<FieldKey, boolean>>>({});
  const scrollViewHeightRef = useRef(0);
  const contentHeightRef = useRef(0);

  // A new waiver version (or reopening) requires reading and signing again
  useEffect(() => {
    if (visible) {
      setHasScrolledToBottom(false);
      setIsProcessing(false);
      setForm(emptyWaiverSignatureForm());
      setTouched({});
    }
  }, [visible, waiver.id]);

  const user = useAuthStore((state) => state.user);
  const profileName = user ? `${user.firstName} ${user.lastName}` : undefined;
  const validation = validateWaiverSignature(form, new Date(), profileName);
  const groupStarted =
    form.minorParticipantName.trim().length > 0 ||
    form.minorDateOfBirth.trim().length > 0 ||
    form.guardianName.trim().length > 0 ||
    form.guardianSignature.trim().length > 0;

  const setField = (key: FieldKey) => (value: string) =>
    setForm((current) => ({ ...current, [key]: value }));
  const blurField = (key: FieldKey) => () =>
    setTouched((current) => ({ ...current, [key]: true }));

  // Inline errors appear once a field has content or has been visited;
  // minor-section errors also appear as soon as the section is started (the
  // all-or-nothing rule must be visible without tabbing through every field).
  // Errors must never require a blur the user has no reason to perform -
  // adult-only signers may never leave the name field.
  const fieldError = (key: FieldKey, inMinorGroup = false): string | undefined => {
    const error = validation.errors[key];
    if (!error) return undefined;
    const hasContent = form[key].trim().length > 0;
    return touched[key] || hasContent || (inMinorGroup && groupStarted) ? error : undefined;
  };

  // Single always-current line under Agree explaining what still blocks it
  const blockingReason = (): string | null => {
    if (!hasScrolledToBottom) return 'Scroll to the bottom to enable Agree';
    if (validation.valid) return null;
    if (validation.errors.participantName) return validation.errors.participantName;
    return 'Complete the Parent/Guardian section - every field is required once started';
  };

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

  const canAgree = hasScrolledToBottom && validation.valid;

  const handleAgree = async () => {
    if (!validation.details) return;
    setIsProcessing(true);
    try {
      await onAgree(validation.details);
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
      <KeyboardAvoidingView
        style={styles.container}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
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
          keyboardShouldPersistTaps="handled"
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
            {parseWaiverSegments(waiver.text).map((segment, i) =>
              segment.bold ? (
                <Text key={i} style={styles.waiverTextBold}>{segment.text}</Text>
              ) : (
                <Text key={i}>{segment.text}</Text>
              )
            )}
          </Text>

          {/* Signature form - revealed once the waiver has been read to the bottom */}
          {hasScrolledToBottom && (
            <View style={styles.form}>
              <Text style={styles.sectionHeader} allowFontScaling={false}>
                Adult Participant
              </Text>
              <SignatureField
                label="Printed Name"
                value={form.participantName}
                onChangeText={setField('participantName')}
                onBlur={blurField('participantName')}
                error={fieldError('participantName')}
                placeholder="Full legal name"
              />
              <ReadOnlyDateField label="Date" />

              <Text style={styles.sectionHeader} allowFontScaling={false}>
                Parent/Guardian (required if participant is under 19)
              </Text>
              <Text style={styles.sectionHint} allowFontScaling={false}>
                Leave this section empty unless the participant is under 19 years
                of age. If you start it, every field is required.
              </Text>
              <SignatureField
                label="Minor Participant's Printed Name"
                value={form.minorParticipantName}
                onChangeText={setField('minorParticipantName')}
                onBlur={blurField('minorParticipantName')}
                error={fieldError('minorParticipantName', true)}
                placeholder="Minor's full legal name"
              />
              <SignatureField
                label="Minor's Date of Birth"
                value={form.minorDateOfBirth}
                onChangeText={setField('minorDateOfBirth')}
                onBlur={blurField('minorDateOfBirth')}
                error={fieldError('minorDateOfBirth', true)}
                placeholder="MM/DD/YYYY"
                isDate
              />
              <SignatureField
                label="Parent/Guardian Printed Name"
                value={form.guardianName}
                onChangeText={setField('guardianName')}
                onBlur={blurField('guardianName')}
                error={fieldError('guardianName', true)}
                placeholder="Parent or guardian's full legal name"
              />
              <SignatureField
                label="Parent/Guardian Signature"
                value={form.guardianSignature}
                onChangeText={setField('guardianSignature')}
                onBlur={blurField('guardianSignature')}
                error={fieldError('guardianSignature', true)}
                placeholder="Type your full name to sign"
                signature
              />
              <ReadOnlyDateField label="Date" />
            </View>
          )}
        </ScrollView>

        <View style={styles.footer}>
          <TouchableOpacity onPress={handleViewPdf} style={styles.pdfLink}>
            <Text style={styles.pdfLinkText} allowFontScaling={false}>
              View / Save as PDF
            </Text>
          </TouchableOpacity>

          {blockingReason() != null && (
            <Text style={styles.scrollHint} allowFontScaling={false}>
              {blockingReason()}
            </Text>
          )}

          <TouchableOpacity
            style={[styles.agreeButton, (!canAgree || isProcessing) && styles.agreeButtonDisabled]}
            onPress={handleAgree}
            disabled={!canAgree || isProcessing}
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
      </KeyboardAvoidingView>
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
  waiverTextBold: {
    fontWeight: '700',
    color: colors.text.primary,
  },
  form: {
    marginTop: spacing.lg,
    paddingTop: spacing.lg,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },
  sectionHeader: {
    fontSize: 15,
    fontWeight: '700',
    color: colors.text.primary,
    marginBottom: spacing.sm,
  },
  sectionHint: {
    fontSize: 13,
    color: colors.text.muted,
    lineHeight: 18,
    marginBottom: spacing.md,
  },
  field: {
    marginBottom: spacing.md,
  },
  fieldLabel: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.secondary,
    marginBottom: spacing.xs,
  },
  fieldInput: {
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.default,
    borderRadius: radius.md,
    padding: spacing.md,
    fontSize: 16,
    color: colors.text.primary,
  },
  // Typed signature rendered in italic to suggest a signature
  signatureInput: {
    fontStyle: 'italic',
    fontSize: 18,
  },
  readOnlyField: {
    opacity: 0.7,
  },
  readOnlyFieldText: {
    fontSize: 16,
    color: colors.text.secondary,
  },
  fieldInputError: {
    borderColor: colors.status.error,
  },
  fieldError: {
    fontSize: 12,
    color: colors.status.error,
    marginTop: spacing.xs,
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
