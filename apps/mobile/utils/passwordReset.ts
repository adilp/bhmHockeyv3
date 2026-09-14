/**
 * Validation for the email password reset screen. Kept out of the component so
 * the rules are testable and match the API (minimum 6 characters, 6-digit code).
 */

export const RESET_CODE_LENGTH = 6;
export const MIN_PASSWORD_LENGTH = 6;

/**
 * Digits only, capped at the code length - so a code pasted as "123 456" or
 * "123-456" from an email still works.
 */
export function normalizeResetCode(input: string): string {
  return input.replace(/\D/g, '').slice(0, RESET_CODE_LENGTH);
}

export function isValidEmail(email: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
}

/**
 * The first problem with a new password, or null when it's acceptable.
 */
export function validateNewPassword(password: string, confirmPassword: string): string | null {
  if (password.length < MIN_PASSWORD_LENGTH) {
    return `Password must be at least ${MIN_PASSWORD_LENGTH} characters`;
  }
  if (password !== confirmPassword) {
    return 'Passwords do not match';
  }
  return null;
}

/**
 * The API client rejects with a plain ApiError object, not an Error, so read
 * its message directly and fall back when there isn't one.
 */
export function getResetErrorMessage(error: unknown, fallback: string): string {
  const message = (error as { message?: unknown } | null)?.message;
  return typeof message === 'string' && message.trim().length > 0 ? message : fallback;
}
