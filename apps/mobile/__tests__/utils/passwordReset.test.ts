import {
  MIN_PASSWORD_LENGTH,
  getResetErrorMessage,
  isValidEmail,
  normalizeResetCode,
  validateNewPassword,
} from '../../utils/passwordReset';

describe('normalizeResetCode', () => {
  it('keeps a plain 6-digit code', () => {
    expect(normalizeResetCode('482913')).toBe('482913');
  });

  it('strips spaces and dashes from a pasted code', () => {
    expect(normalizeResetCode('482 913')).toBe('482913');
    expect(normalizeResetCode('482-913')).toBe('482913');
  });

  it('caps at six digits', () => {
    expect(normalizeResetCode('48291377')).toBe('482913');
  });

  it('drops non-digits entirely', () => {
    expect(normalizeResetCode('abc')).toBe('');
  });
});

describe('isValidEmail', () => {
  it('accepts a normal address, ignoring surrounding space', () => {
    expect(isValidEmail('  britt@example.com ')).toBe(true);
  });

  it.each(['', 'britt', 'britt@', '@example.com', 'britt@example', 'brit t@example.com'])(
    'rejects %p',
    (value) => {
      expect(isValidEmail(value)).toBe(false);
    }
  );
});

describe('validateNewPassword', () => {
  it('accepts a long-enough matching password', () => {
    expect(validateNewPassword('hockey1', 'hockey1')).toBeNull();
  });

  it('rejects a password shorter than the API minimum', () => {
    const short = 'x'.repeat(MIN_PASSWORD_LENGTH - 1);
    expect(validateNewPassword(short, short)).toMatch(/at least 6/);
  });

  it('rejects a mismatch', () => {
    expect(validateNewPassword('hockey1', 'hockey2')).toBe('Passwords do not match');
  });

  it('reports length before mismatch', () => {
    expect(validateNewPassword('abc', 'xyz')).toMatch(/at least/);
  });
});

describe('getResetErrorMessage', () => {
  it('reads the message off an ApiError-shaped object', () => {
    expect(getResetErrorMessage({ message: 'This reset link or code is invalid', statusCode: 400 }, 'fallback'))
      .toBe('This reset link or code is invalid');
  });

  it('falls back when there is no usable message', () => {
    expect(getResetErrorMessage(null, 'fallback')).toBe('fallback');
    expect(getResetErrorMessage({ message: '  ' }, 'fallback')).toBe('fallback');
    expect(getResetErrorMessage({ statusCode: 500 }, 'fallback')).toBe('fallback');
  });
});
