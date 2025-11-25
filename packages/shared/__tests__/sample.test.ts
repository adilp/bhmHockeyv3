/**
 * Sample test to verify Jest is configured correctly.
 * Delete this file after adding real tests.
 */
describe('Test Framework Setup', () => {
  it('should run basic assertions', () => {
    expect(2 + 2).toBe(4);
  });

  it('should handle arrays', () => {
    const items = ['hockey', 'puck', 'rink'];
    expect(items).toHaveLength(3);
    expect(items).toContain('hockey');
  });

  it('should handle async operations', async () => {
    const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));
    await delay(10);
    expect(true).toBe(true);
  });
});
