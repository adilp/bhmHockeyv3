import { Linking, Alert } from 'react-native';

/**
 * Venmo URL scheme for payments
 *
 * Parameters:
 * - txn: 'pay' for payments
 * - recipients: Venmo username (without @)
 * - amount: Payment amount
 * - note: Payment description
 *
 * Note: Venmo deep-link parameters are not officially documented
 * by Venmo and may change. This is a "best-effort convenience" feature.
 * The same pattern is used by apps like Splitwise.
 */
export function buildVenmoPaymentUrl(
  venmoHandle: string,
  amount: number,
  note: string
): string {
  // Remove @ prefix if present
  const handle = venmoHandle.startsWith('@') ? venmoHandle.slice(1) : venmoHandle;

  // URL encode the note
  const encodedNote = encodeURIComponent(note);

  // Venmo deep link format
  return `venmo://paycharge?txn=pay&recipients=${handle}&amount=${amount.toFixed(2)}&note=${encodedNote}`;
}

/**
 * Build fallback web URL for Venmo (if app not installed)
 */
export function buildVenmoWebUrl(venmoHandle: string): string {
  const handle = venmoHandle.startsWith('@') ? venmoHandle.slice(1) : venmoHandle;
  return `https://venmo.com/${handle}`;
}

/**
 * Open Venmo app with pre-filled payment details
 * Falls back to web URL if Venmo app is not installed
 */
export async function openVenmoPayment(
  venmoHandle: string,
  amount: number,
  eventName: string,
  eventDate?: string
): Promise<boolean> {
  const formattedDate = eventDate
    ? new Date(eventDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
    : '';
  const detail = eventName
    ? `${eventName}${formattedDate ? ` (${formattedDate})` : ''}`
    : formattedDate || 'Hockey';
  const note = `BHM Hockey: ${detail}`;
  const venmoUrl = buildVenmoPaymentUrl(venmoHandle, amount, note);
  const webUrl = buildVenmoWebUrl(venmoHandle);

  try {
    // Try to open Venmo app directly
    // Note: canOpenURL doesn't work in Expo Go for custom schemes
    // In production builds with LSApplicationQueriesSchemes configured, this will work properly
    await Linking.openURL(venmoUrl);
    return true;
  } catch (error) {
    // Venmo app not installed or URL scheme not supported
    console.log('Could not open Venmo app, offering web fallback:', error);
    Alert.alert(
      'Open Venmo',
      'Would you like to open Venmo in your browser?',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Open Browser',
          onPress: async () => {
            try {
              await Linking.openURL(webUrl);
            } catch (webError) {
              console.error('Error opening web URL:', webError);
            }
          },
        },
      ]
    );
    return false;
  }
}

/**
 * Get payment status display info
 */
export function getPaymentStatusInfo(status: string | null | undefined): {
  label: string;
  color: string;
  backgroundColor: string;
} {
  switch (status) {
    case 'Pending':
      return {
        label: 'Payment Pending',
        color: '#856404',
        backgroundColor: '#FFF3CD',
      };
    case 'MarkedPaid':
      return {
        label: 'Awaiting Verification',
        color: '#0C5460',
        backgroundColor: '#D1ECF1',
      };
    case 'Verified':
      return {
        label: 'Payment Verified',
        color: '#155724',
        backgroundColor: '#D4EDDA',
      };
    default:
      return {
        label: 'Free Event',
        color: '#666',
        backgroundColor: '#E9ECEF',
      };
  }
}
