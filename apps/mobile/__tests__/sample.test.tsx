/**
 * Sample test to verify Jest is configured correctly for the mobile app.
 * Delete this file after adding real tests.
 */
describe('Mobile App Test Framework Setup', () => {
  it('should run basic assertions', () => {
    expect(2 + 2).toBe(4);
  });

  it('should handle objects', () => {
    const user = {
      name: 'Test User',
      email: 'test@example.com',
    };
    expect(user).toHaveProperty('name');
    expect(user.email).toContain('@');
  });

  it('should handle async operations', async () => {
    const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));
    await delay(10);
    expect(true).toBe(true);
  });

  it('should mock react-native Platform', () => {
    const { Platform } = require('react-native');
    expect(Platform.OS).toBe('ios');
  });
});
