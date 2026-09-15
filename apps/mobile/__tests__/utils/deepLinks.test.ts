const mockPush = jest.fn();

jest.mock('expo-router', () => ({
  router: { push: (...args: unknown[]) => mockPush(...args) },
}));

jest.mock('expo-linking', () => ({
  parse: (url: string) => {
    const parsed = new URL(url.replace(/^bhmhockey:\/\//, 'https://app.local/'));
    const path = parsed.pathname.replace(/^\//, '');
    return { path: path || null, queryParams: Object.fromEntries(parsed.searchParams) };
  },
  getInitialURL: jest.fn(),
  addEventListener: jest.fn(),
}));

import { handleDeepLink } from '../../utils/deepLinks';

describe('handleDeepLink', () => {
  beforeEach(() => {
    mockPush.mockClear();
    jest.spyOn(console, 'log').mockImplementation(() => {});
  });

  afterEach(() => {
    (console.log as jest.Mock).mockRestore();
  });

  it('leaves password reset links to Expo Router, so the screen is not opened twice', () => {
    handleDeepLink('bhmhockey://reset-password?token=Ab3_-Ab3_-Ab3_-Ab3_-Ab3_-Ab3_-Ab3_-');

    expect(mockPush).not.toHaveBeenCalled();
  });

  it('still routes organization links', () => {
    handleDeepLink('bhmhockey://organizations/abc-123');

    expect(mockPush).toHaveBeenCalledWith('/organizations/abc-123');
  });

  it('still routes tournament team links', () => {
    handleDeepLink('bhmhockey://tournament/t1/team/team9');

    expect(mockPush).toHaveBeenCalledWith('/tournaments/t1/teams/team9');
  });
});
