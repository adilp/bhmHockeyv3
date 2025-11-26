/**
 * Validation Tests - Ensuring our validation rules work correctly
 * These tests prevent invalid data from reaching the API.
 */
import { VALIDATION } from '../src/constants';

const { EMAIL_REGEX, PASSWORD_REGEX, PHONE_REGEX, PASSWORD_MIN_LENGTH } = VALIDATION;

describe('Validation Rules', () => {
  describe('EMAIL_REGEX', () => {
    it.each([
      'test@example.com',
      'user.name@domain.org',
      'user+tag@example.co.uk',
      'firstname.lastname@company.com',
      'test123@test.io',
    ])('accepts valid email: %s', (email) => {
      expect(EMAIL_REGEX.test(email)).toBe(true);
    });

    it.each([
      'invalid',
      '@nodomain.com',
      'no@domain',
      'spaces in@email.com',
      '',
      'missing@.com',
      '@.com',
      'double@@at.com',
    ])('rejects invalid email: %s', (email) => {
      expect(EMAIL_REGEX.test(email)).toBe(false);
    });
  });

  describe('PASSWORD_REGEX', () => {
    it('accepts password meeting all requirements', () => {
      expect(PASSWORD_REGEX.test('Password1!')).toBe(true);
      expect(PASSWORD_REGEX.test('Test@123')).toBe(true);
      expect(PASSWORD_REGEX.test('SecureP@ss1')).toBe(true);
    });

    it('rejects password without uppercase', () => {
      expect(PASSWORD_REGEX.test('password1!')).toBe(false);
    });

    it('rejects password without lowercase', () => {
      expect(PASSWORD_REGEX.test('PASSWORD1!')).toBe(false);
    });

    it('rejects password without number', () => {
      expect(PASSWORD_REGEX.test('Password!')).toBe(false);
    });

    it('rejects password without special character', () => {
      expect(PASSWORD_REGEX.test('Password1')).toBe(false);
    });

    it('PASSWORD_MIN_LENGTH is 8', () => {
      expect(PASSWORD_MIN_LENGTH).toBe(8);
    });

    it('rejects empty password', () => {
      expect(PASSWORD_REGEX.test('')).toBe(false);
    });
  });

  describe('PHONE_REGEX', () => {
    it('accepts 10 digit phone number', () => {
      expect(PHONE_REGEX.test('1234567890')).toBe(true);
      expect(PHONE_REGEX.test('0000000000')).toBe(true);
      expect(PHONE_REGEX.test('9876543210')).toBe(true);
    });

    it('rejects phone with wrong digit count', () => {
      expect(PHONE_REGEX.test('123456789')).toBe(false);  // 9 digits
      expect(PHONE_REGEX.test('12345678901')).toBe(false); // 11 digits
      expect(PHONE_REGEX.test('')).toBe(false); // empty
      expect(PHONE_REGEX.test('12345')).toBe(false); // too short
    });

    it('rejects phone with non-numeric characters', () => {
      expect(PHONE_REGEX.test('123-456-7890')).toBe(false);
      expect(PHONE_REGEX.test('(123)4567890')).toBe(false);
      expect(PHONE_REGEX.test('123 456 7890')).toBe(false);
      expect(PHONE_REGEX.test('abcdefghij')).toBe(false);
    });
  });
});
