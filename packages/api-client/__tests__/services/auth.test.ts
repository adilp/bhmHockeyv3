/**
 * AuthService Tests - Verifying the API client correctly stores and clears tokens
 * These tests protect token management which is critical for auth security.
 */

// Store mock functions at the top level so we can reference them in tests
const mockSetItem = jest.fn<Promise<void>, [string, string]>(() => Promise.resolve());
const mockGetItem = jest.fn<Promise<string | null>, [string]>(() => Promise.resolve(null));
const mockRemoveItem = jest.fn<Promise<void>, [string]>(() => Promise.resolve());

// Mock AsyncStorage before any imports
jest.mock('@react-native-async-storage/async-storage', () => ({
  setItem: mockSetItem,
  getItem: mockGetItem,
  removeItem: mockRemoveItem,
}));

// Store axios mock functions
const mockPost = jest.fn();
const mockGet = jest.fn();
const mockPut = jest.fn();

// Mock axios
jest.mock('axios', () => ({
  create: jest.fn(() => ({
    interceptors: {
      request: { use: jest.fn() },
      response: { use: jest.fn() },
    },
    post: mockPost,
    get: mockGet,
    put: mockPut,
  })),
}));

// Import after mocking
import { authStorage } from '../../src/storage/auth';
import { authService } from '../../src/services/auth';
import { initializeApiClient } from '../../src/client';

describe('authStorage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('setToken', () => {
    it('stores token in AsyncStorage', async () => {
      await authStorage.setToken('test-jwt-token');

      expect(mockSetItem).toHaveBeenCalledWith(
        '@bhmhockey:authToken',
        'test-jwt-token'
      );
    });
  });

  describe('setRefreshToken', () => {
    it('stores refresh token in AsyncStorage', async () => {
      await authStorage.setRefreshToken('test-refresh-token');

      expect(mockSetItem).toHaveBeenCalledWith(
        '@bhmhockey:refreshToken',
        'test-refresh-token'
      );
    });
  });

  describe('getToken', () => {
    it('retrieves token from AsyncStorage', async () => {
      mockGetItem.mockResolvedValueOnce('stored-token');

      const result = await authStorage.getToken();

      expect(mockGetItem).toHaveBeenCalledWith('@bhmhockey:authToken');
      expect(result).toBe('stored-token');
    });

    it('returns null when no token exists', async () => {
      mockGetItem.mockResolvedValueOnce(null);

      const result = await authStorage.getToken();

      expect(result).toBeNull();
    });

    it('returns null on error', async () => {
      mockGetItem.mockRejectedValueOnce(new Error('Storage error'));

      const result = await authStorage.getToken();

      expect(result).toBeNull();
    });
  });

  describe('removeToken', () => {
    it('removes both tokens from AsyncStorage', async () => {
      await authStorage.removeToken();

      expect(mockRemoveItem).toHaveBeenCalledWith('@bhmhockey:authToken');
      expect(mockRemoveItem).toHaveBeenCalledWith('@bhmhockey:refreshToken');
    });
  });

  describe('getRefreshToken', () => {
    it('retrieves refresh token from AsyncStorage', async () => {
      mockGetItem.mockResolvedValueOnce('stored-refresh');

      const result = await authStorage.getRefreshToken();

      expect(mockGetItem).toHaveBeenCalledWith('@bhmhockey:refreshToken');
      expect(result).toBe('stored-refresh');
    });
  });
});

describe('authService', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    // Initialize API client for each test
    initializeApiClient({ baseURL: 'http://localhost:5001/api' });
  });

  describe('login', () => {
    it('stores token after successful login', async () => {
      const mockResponse = {
        data: {
          token: 'new-jwt-token',
          refreshToken: 'new-refresh-token',
          user: { id: '1', email: 'test@example.com' },
        },
      };
      mockPost.mockResolvedValueOnce(mockResponse);

      await authService.login({ email: 'test@example.com', password: 'pass' });

      expect(mockSetItem).toHaveBeenCalledWith(
        '@bhmhockey:authToken',
        'new-jwt-token'
      );
      expect(mockSetItem).toHaveBeenCalledWith(
        '@bhmhockey:refreshToken',
        'new-refresh-token'
      );
    });

    it('returns auth response on success', async () => {
      const mockResponse = {
        data: {
          token: 'jwt-token',
          refreshToken: 'refresh-token',
          user: { id: '1', email: 'test@example.com' },
        },
      };
      mockPost.mockResolvedValueOnce(mockResponse);

      const result = await authService.login({
        email: 'test@example.com',
        password: 'pass',
      });

      expect(result).toEqual(mockResponse.data);
    });

    it('calls API with correct endpoint and credentials', async () => {
      const mockResponse = {
        data: {
          token: 'token',
          refreshToken: 'refresh',
          user: { id: '1', email: 'test@example.com' },
        },
      };
      mockPost.mockResolvedValueOnce(mockResponse);

      await authService.login({ email: 'test@example.com', password: 'pass' });

      expect(mockPost).toHaveBeenCalledWith('/auth/login', {
        email: 'test@example.com',
        password: 'pass',
      });
    });
  });

  describe('register', () => {
    it('stores tokens after successful registration', async () => {
      const mockResponse = {
        data: {
          token: 'new-jwt-token',
          refreshToken: 'new-refresh-token',
          user: { id: '1', email: 'test@example.com' },
        },
      };
      mockPost.mockResolvedValueOnce(mockResponse);

      await authService.register({
        email: 'test@example.com',
        password: 'Password1!',
        firstName: 'Test',
        lastName: 'User',
      });

      expect(mockSetItem).toHaveBeenCalledWith(
        '@bhmhockey:authToken',
        'new-jwt-token'
      );
    });

    it('calls API with correct endpoint and data', async () => {
      const mockResponse = {
        data: {
          token: 'token',
          refreshToken: 'refresh',
          user: { id: '1', email: 'test@example.com' },
        },
      };
      mockPost.mockResolvedValueOnce(mockResponse);

      const registerData = {
        email: 'test@example.com',
        password: 'Password1!',
        firstName: 'Test',
        lastName: 'User',
      };
      await authService.register(registerData);

      expect(mockPost).toHaveBeenCalledWith('/auth/register', registerData);
    });
  });

  describe('logout', () => {
    it('removes tokens from storage', async () => {
      mockPost.mockResolvedValueOnce({});

      await authService.logout();

      expect(mockRemoveItem).toHaveBeenCalled();
    });

    it('removes tokens even if API call fails', async () => {
      mockPost.mockRejectedValueOnce(new Error('Network error'));

      await authService.logout();

      // Tokens should still be removed
      expect(mockRemoveItem).toHaveBeenCalled();
    });

    it('calls logout endpoint', async () => {
      mockPost.mockResolvedValueOnce({});

      await authService.logout();

      expect(mockPost).toHaveBeenCalledWith('/auth/logout');
    });
  });

  describe('isAuthenticated', () => {
    it('returns true when token exists', async () => {
      mockGetItem.mockResolvedValueOnce('some-token');

      const result = await authService.isAuthenticated();

      expect(result).toBe(true);
    });

    it('returns false when no token exists', async () => {
      mockGetItem.mockResolvedValueOnce(null);

      const result = await authService.isAuthenticated();

      expect(result).toBe(false);
    });
  });

  describe('getCurrentUser', () => {
    it('fetches current user from API', async () => {
      const mockUser = { id: '1', email: 'test@example.com' };
      mockGet.mockResolvedValueOnce({ data: mockUser });

      const result = await authService.getCurrentUser();

      expect(mockGet).toHaveBeenCalledWith('/users/me');
      expect(result).toEqual(mockUser);
    });
  });

  describe('updatePushToken', () => {
    it('calls API with push token', async () => {
      mockPut.mockResolvedValueOnce({});

      await authService.updatePushToken('ExponentPushToken[xxxxx]');

      expect(mockPut).toHaveBeenCalledWith('/users/me/push-token', {
        pushToken: 'ExponentPushToken[xxxxx]',
      });
    });
  });
});
