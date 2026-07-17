/**
 * OrganizationService Tests - Verifying the API client calls the correct
 * auto-roster endpoints with the correct payloads.
 */

// Mock AsyncStorage before any imports
jest.mock('@react-native-async-storage/async-storage', () => ({
  setItem: jest.fn(() => Promise.resolve()),
  getItem: jest.fn(() => Promise.resolve(null)),
  removeItem: jest.fn(() => Promise.resolve()),
}));

// Store axios mock functions
const mockPost = jest.fn();
const mockGet = jest.fn();
const mockPut = jest.fn();
const mockDelete = jest.fn();

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
    delete: mockDelete,
  })),
}));

// Import after mocking
import { organizationService } from '../../src/services/organizations';
import { initializeApiClient } from '../../src/client';

const mockMember = {
  id: 'member-1',
  userId: 'user-1',
  firstName: 'Test',
  lastName: 'User',
  positions: { skater: 'Silver' },
  position: 'Skater',
  sortOrder: 0,
  addedAt: '2026-07-16T00:00:00Z',
};

describe('organizationService auto-roster', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    initializeApiClient({ baseURL: 'http://localhost:5001/api' });
  });

  describe('getAutoRoster', () => {
    it('fetches the auto-roster from the correct endpoint', async () => {
      mockGet.mockResolvedValueOnce({ data: [mockMember] });

      const result = await organizationService.getAutoRoster('org-1');

      expect(mockGet).toHaveBeenCalledWith('/organizations/org-1/auto-roster');
      expect(result).toEqual([mockMember]);
    });
  });

  describe('addAutoRosterMember', () => {
    it('posts the user and position to the correct endpoint', async () => {
      mockPost.mockResolvedValueOnce({ data: mockMember });

      const result = await organizationService.addAutoRosterMember('org-1', {
        userId: 'user-1',
        position: 'Skater',
      });

      expect(mockPost).toHaveBeenCalledWith('/organizations/org-1/auto-roster', {
        userId: 'user-1',
        position: 'Skater',
      });
      expect(result).toEqual(mockMember);
    });
  });

  describe('removeAutoRosterMember', () => {
    it('deletes the member by user id', async () => {
      mockDelete.mockResolvedValueOnce({});

      await organizationService.removeAutoRosterMember('org-1', 'user-1');

      expect(mockDelete).toHaveBeenCalledWith('/organizations/org-1/auto-roster/user-1');
    });
  });

  describe('reorderAutoRoster', () => {
    it('puts the ordered user ids to the order endpoint', async () => {
      mockPut.mockResolvedValueOnce({ data: [mockMember] });

      const result = await organizationService.reorderAutoRoster('org-1', ['user-2', 'user-1']);

      expect(mockPut).toHaveBeenCalledWith('/organizations/org-1/auto-roster/order', {
        orderedUserIds: ['user-2', 'user-1'],
      });
      expect(result).toEqual([mockMember]);
    });
  });
});
