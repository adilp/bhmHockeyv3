import type { WaiverSignatureDetails } from '@bhmhockey/shared';

/**
 * Waiver acceptance signature form: MM/DD/YYYY date helpers and the
 * client-side validation rules (mirrored server-side in
 * OrganizationWaiverService.ValidateSignatureFields):
 * - Printed name required (non-empty trimmed)
 * - Parent/Guardian section is all-or-nothing; when active, the minor's
 *   date of birth must be a valid date in the past
 * Signature dates are displayed read-only (always today) and stamped by
 * the server - they are not part of the form payload.
 */

// A parsed calendar date (no time component, no timezone)
interface CalendarDate {
  year: number;
  month: number; // 1-12
  day: number;   // 1-31
}

/** Today's date on the local device clock, formatted MM/DD/YYYY */
export function todayMMDDYYYY(now: Date = new Date()): string {
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${month}/${day}/${now.getFullYear()}`;
}

/**
 * Parse an MM/DD/YYYY input (1-2 digit month/day accepted, 4-digit year
 * required) into a calendar date. Returns null for anything that is not a
 * real calendar date (e.g. 02/30/2026).
 */
export function parseDateInput(value: string): CalendarDate | null {
  const match = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(value.trim());
  if (!match) {
    return null;
  }

  const month = parseInt(match[1], 10);
  const day = parseInt(match[2], 10);
  const year = parseInt(match[3], 10);
  if (month < 1 || month > 12) {
    return null;
  }
  // Date.UTC never throws - verify no rollover (e.g. 02/30 -> 03/02)
  const check = new Date(Date.UTC(year, month - 1, day));
  if (
    check.getUTCFullYear() !== year ||
    check.getUTCMonth() !== month - 1 ||
    check.getUTCDate() !== day
  ) {
    return null;
  }

  return { year, month, day };
}

/** ISO calendar date (YYYY-MM-DD) - the wire format the API expects */
export function toIsoDate(date: CalendarDate): string {
  const month = String(date.month).padStart(2, '0');
  const day = String(date.day).padStart(2, '0');
  return `${date.year}-${month}-${day}`;
}

/** True when the date is strictly before today on the local device clock */
export function isPastDate(date: CalendarDate, now: Date = new Date()): boolean {
  const value = date.year * 10000 + date.month * 100 + date.day;
  const today = now.getFullYear() * 10000 + (now.getMonth() + 1) * 100 + now.getDate();
  return value < today;
}

// Raw form values as typed by the user (all plain text inputs)
export interface WaiverSignatureFormValues {
  participantName: string;
  minorParticipantName: string;
  minorDateOfBirth: string;
  guardianName: string;
  guardianSignature: string;
}

export const emptyWaiverSignatureForm = (): WaiverSignatureFormValues => ({
  participantName: '',
  minorParticipantName: '',
  minorDateOfBirth: '',
  guardianName: '',
  guardianSignature: '',
});

export interface WaiverSignatureValidation {
  valid: boolean;
  errors: Partial<Record<keyof WaiverSignatureFormValues, string>>;
  /** API payload - only present when valid */
  details: WaiverSignatureDetails | null;
}

const DATE_ERROR = 'Enter a valid date (MM/DD/YYYY)';

/**
 * Validate the acceptance form and build the API payload.
 *
 * The Parent/Guardian group counts as "started" when any of its typed fields
 * (minor name, date of birth, guardian name, signature) is non-empty. When
 * the group is empty, no minor fields are sent. Signature dates are not
 * validated or sent - the server stamps them at acceptance time.
 */
export function validateWaiverSignature(
  values: WaiverSignatureFormValues,
  now: Date = new Date()
): WaiverSignatureValidation {
  const errors: WaiverSignatureValidation['errors'] = {};

  const participantName = values.participantName.trim();
  if (!participantName) {
    errors.participantName = 'Printed name is required';
  }

  const minorParticipantName = values.minorParticipantName.trim();
  const minorDateOfBirthText = values.minorDateOfBirth.trim();
  const guardianName = values.guardianName.trim();
  const guardianSignature = values.guardianSignature.trim();

  const groupStarted =
    minorParticipantName.length > 0 ||
    minorDateOfBirthText.length > 0 ||
    guardianName.length > 0 ||
    guardianSignature.length > 0;

  let minorDateOfBirth: CalendarDate | null = null;

  if (groupStarted) {
    if (!minorParticipantName) {
      errors.minorParticipantName = "Minor's name is required to complete this section";
    }
    if (!minorDateOfBirthText) {
      errors.minorDateOfBirth = 'Date of birth is required to complete this section';
    } else {
      minorDateOfBirth = parseDateInput(minorDateOfBirthText);
      if (!minorDateOfBirth) {
        errors.minorDateOfBirth = DATE_ERROR;
      } else if (!isPastDate(minorDateOfBirth, now)) {
        errors.minorDateOfBirth = 'Date of birth must be in the past';
      }
    }
    if (!guardianName) {
      errors.guardianName = 'Parent/guardian name is required to complete this section';
    }
    if (!guardianSignature) {
      errors.guardianSignature = 'Signature is required to complete this section';
    }
  }

  if (Object.keys(errors).length > 0) {
    return { valid: false, errors, details: null };
  }

  const details: WaiverSignatureDetails = {
    participantName,
    ...(groupStarted
      ? {
          minorParticipantName,
          minorDateOfBirth: toIsoDate(minorDateOfBirth!),
          guardianName,
          guardianSignature,
        }
      : {}),
  };

  return { valid: true, errors: {}, details };
}
