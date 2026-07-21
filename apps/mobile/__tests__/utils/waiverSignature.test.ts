import {
  emptyWaiverSignatureForm,
  isPastDate,
  parseDateInput,
  todayMMDDYYYY,
  toIsoDate,
  validateWaiverSignature,
  type WaiverSignatureFormValues,
} from '../../utils/waiverSignature';

// Fixed "now" so date-relative assertions are deterministic
const NOW = new Date(2026, 6, 21); // July 21, 2026 (local)

const validAdultForm = (): WaiverSignatureFormValues => ({
  ...emptyWaiverSignatureForm(),
  participantName: 'Jane Skater',
});

const validMinorForm = (): WaiverSignatureFormValues => ({
  ...validAdultForm(),
  minorParticipantName: 'Minor Player',
  minorDateOfBirth: '03/05/2014',
  guardianName: 'Pat Guardian',
  guardianSignature: 'Pat Guardian',
});

describe('todayMMDDYYYY', () => {
  it('formats the local date as MM/DD/YYYY with zero padding', () => {
    expect(todayMMDDYYYY(new Date(2026, 6, 21))).toBe('07/21/2026');
    expect(todayMMDDYYYY(new Date(2026, 11, 5))).toBe('12/05/2026');
  });
});

describe('parseDateInput', () => {
  it('parses MM/DD/YYYY', () => {
    expect(parseDateInput('07/21/2026')).toEqual({ year: 2026, month: 7, day: 21 });
  });

  it('accepts 1-digit month and day', () => {
    expect(parseDateInput('7/4/2026')).toEqual({ year: 2026, month: 7, day: 4 });
  });

  it('trims surrounding whitespace', () => {
    expect(parseDateInput(' 07/21/2026 ')).toEqual({ year: 2026, month: 7, day: 21 });
  });

  it.each([
    ['empty', ''],
    ['garbage', 'not a date'],
    ['2-digit year', '07/21/26'],
    ['ISO format', '2026-07-21'],
    ['month 13', '13/01/2026'],
    ['month 0', '0/10/2026'],
    ['day rollover', '02/30/2026'],
    ['day 32', '01/32/2026'],
    ['missing year', '07/21'],
  ])('rejects %s', (_label, value) => {
    expect(parseDateInput(value)).toBeNull();
  });

  it('accepts Feb 29 only in leap years', () => {
    expect(parseDateInput('02/29/2024')).toEqual({ year: 2024, month: 2, day: 29 });
    expect(parseDateInput('02/29/2026')).toBeNull();
  });
});

describe('toIsoDate', () => {
  it('formats as YYYY-MM-DD with zero padding', () => {
    expect(toIsoDate({ year: 2026, month: 7, day: 4 })).toBe('2026-07-04');
  });
});

describe('isPastDate', () => {
  it('is true for yesterday, false for today and tomorrow', () => {
    expect(isPastDate({ year: 2026, month: 7, day: 20 }, NOW)).toBe(true);
    expect(isPastDate({ year: 2026, month: 7, day: 21 }, NOW)).toBe(false);
    expect(isPastDate({ year: 2026, month: 7, day: 22 }, NOW)).toBe(false);
  });
});

describe('emptyWaiverSignatureForm', () => {
  it('starts with every typed field empty (signature dates are server-stamped, not form fields)', () => {
    const form = emptyWaiverSignatureForm();
    expect(form).toEqual({
      participantName: '',
      minorParticipantName: '',
      minorDateOfBirth: '',
      guardianName: '',
      guardianSignature: '',
    });
  });
});

describe('validateWaiverSignature', () => {
  describe('adult participant', () => {
    it('accepts a valid adult-only form and builds the payload without minor fields or dates', () => {
      const result = validateWaiverSignature(validAdultForm(), NOW);

      expect(result.valid).toBe(true);
      expect(result.errors).toEqual({});
      expect(result.details).toEqual({
        participantName: 'Jane Skater',
      });
    });

    it('trims the participant name in the payload', () => {
      const result = validateWaiverSignature(
        { ...validAdultForm(), participantName: '  Jane Skater  ' },
        NOW
      );

      expect(result.details?.participantName).toBe('Jane Skater');
    });

    it.each([
      ['empty', ''],
      ['whitespace-only', '   '],
    ])('requires the printed name (%s)', (_label, participantName) => {
      const result = validateWaiverSignature({ ...validAdultForm(), participantName }, NOW);

      expect(result.valid).toBe(false);
      expect(result.errors.participantName).toBe('Printed name is required');
      expect(result.details).toBeNull();
    });
  });

  describe('Parent/Guardian section (all-or-nothing)', () => {
    it('accepts a fully filled minor section and includes it in the payload (no dates)', () => {
      const result = validateWaiverSignature(validMinorForm(), NOW);

      expect(result.valid).toBe(true);
      expect(result.details).toEqual({
        participantName: 'Jane Skater',
        minorParticipantName: 'Minor Player',
        minorDateOfBirth: '2014-03-05',
        guardianName: 'Pat Guardian',
        guardianSignature: 'Pat Guardian',
      });
    });

    it('treats an untouched section as empty', () => {
      const result = validateWaiverSignature(validAdultForm(), NOW);

      expect(result.valid).toBe(true);
      expect(result.details?.minorParticipantName).toBeUndefined();
      expect(result.details?.minorDateOfBirth).toBeUndefined();
    });

    it.each([
      ['minor name', { minorParticipantName: 'Minor Player' }],
      ['minor date of birth', { minorDateOfBirth: '03/05/2014' }],
      ['guardian name', { guardianName: 'Pat Guardian' }],
      ['guardian signature', { guardianSignature: 'Pat Guardian' }],
    ])('filling only the %s marks the other minor fields as required', (_label, partial) => {
      const result = validateWaiverSignature({ ...validAdultForm(), ...partial }, NOW);

      expect(result.valid).toBe(false);
      expect(result.details).toBeNull();
      // Every missing group field gets its own inline error
      const groupKeys = [
        'minorParticipantName',
        'minorDateOfBirth',
        'guardianName',
        'guardianSignature',
      ] as const;
      for (const key of groupKeys) {
        if (key in partial) {
          expect(result.errors[key]).toBeUndefined();
        } else {
          expect(result.errors[key]).toBeDefined();
        }
      }
    });

    it.each([
      ['today', '07/21/2026'],
      ['the future', '01/01/2030'],
    ])("rejects a minor date of birth set to %s", (_label, minorDateOfBirth) => {
      const result = validateWaiverSignature({ ...validMinorForm(), minorDateOfBirth }, NOW);

      expect(result.valid).toBe(false);
      expect(result.errors.minorDateOfBirth).toBe('Date of birth must be in the past');
    });

    it('rejects an unparseable minor date of birth', () => {
      const result = validateWaiverSignature({ ...validMinorForm(), minorDateOfBirth: 'nope' }, NOW);

      expect(result.valid).toBe(false);
      expect(result.errors.minorDateOfBirth).toBe('Enter a valid date (MM/DD/YYYY)');
    });

    it('trims minor-section strings in the payload', () => {
      const result = validateWaiverSignature(
        {
          ...validMinorForm(),
          minorParticipantName: ' Minor Player ',
          guardianSignature: '  Pat Guardian ',
        },
        NOW
      );

      expect(result.details?.minorParticipantName).toBe('Minor Player');
      expect(result.details?.guardianSignature).toBe('Pat Guardian');
    });
  });
});
