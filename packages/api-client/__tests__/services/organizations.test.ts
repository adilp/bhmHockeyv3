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

const mockWaiver = {
  id: 'waiver-1',
  organizationId: 'org-1',
  text: 'You play at your own risk.',
  version: 2,
  createdAt: '2026-07-16T00:00:00Z',
};

describe('organizationService waivers', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    initializeApiClient({ baseURL: 'http://localhost:5001/api' });
  });

  describe('getWaiver', () => {
    it('fetches the current waiver from the correct endpoint', async () => {
      mockGet.mockResolvedValueOnce({ data: mockWaiver });

      const result = await organizationService.getWaiver('org-1');

      expect(mockGet).toHaveBeenCalledWith('/organizations/org-1/waiver');
      expect(result).toEqual(mockWaiver);
    });
  });

  describe('setWaiver', () => {
    it('puts the text and returns the new active waiver', async () => {
      mockPut.mockResolvedValueOnce({ data: { waiver: mockWaiver } });

      const result = await organizationService.setWaiver('org-1', 'You play at your own risk.');

      expect(mockPut).toHaveBeenCalledWith('/organizations/org-1/waiver', {
        text: 'You play at your own risk.',
      });
      expect(result).toEqual(mockWaiver);
    });

    it('returns null when the waiver was cleared', async () => {
      mockPut.mockResolvedValueOnce({ data: { waiver: null } });

      const result = await organizationService.setWaiver('org-1', '');

      expect(mockPut).toHaveBeenCalledWith('/organizations/org-1/waiver', { text: '' });
      expect(result).toBeNull();
    });
  });

  describe('acceptWaiver', () => {
    it('posts the specific waiver id to the accept endpoint', async () => {
      mockPost.mockResolvedValueOnce({});

      await organizationService.acceptWaiver('org-1', 'waiver-1');

      expect(mockPost).toHaveBeenCalledWith('/organizations/org-1/waiver/accept', {
        waiverId: 'waiver-1',
      });
    });
  });

  describe('getPendingWaivers', () => {
    it('fetches pending waivers for the current user', async () => {
      const pending = [
        { organizationId: 'org-1', organizationName: 'Test Org', waiver: mockWaiver },
      ];
      mockGet.mockResolvedValueOnce({ data: pending });

      const result = await organizationService.getPendingWaivers();

      expect(mockGet).toHaveBeenCalledWith('/users/me/pending-waivers');
      expect(result).toEqual(pending);
    });
  });

  describe('leaveOrganization', () => {
    it('posts to the leave endpoint', async () => {
      mockPost.mockResolvedValueOnce({});

      await organizationService.leaveOrganization('org-1');

      expect(mockPost).toHaveBeenCalledWith('/organizations/org-1/leave');
    });
  });
});
