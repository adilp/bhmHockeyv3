// Test that tournament admin types are properly exported from main index
import {
  TournamentAdminRole,
  TournamentAdminDto,
  AddTournamentAdminRequest,
  UpdateTournamentAdminRoleRequest,
  TransferOwnershipRequest,
} from '../index';

describe('Tournament Admin Type Exports', () => {
  it('should export TournamentAdminDto from main index', () => {
    const admin: TournamentAdminDto = {
      id: 'admin-123',
      tournamentId: 'tournament-456',
      userId: 'user-789',
      userFirstName: 'John',
      userLastName: 'Doe',
      userEmail: 'john@example.com',
      role: 'Admin',
      addedAt: '2026-01-18T00:00:00Z',
      addedByUserId: 'creator-123',
      addedByName: 'Jane Smith',
    };
    expect(admin).toBeDefined();
  });

  it('should export AddTournamentAdminRequest from main index', () => {
    const request: AddTournamentAdminRequest = {
      userId: 'user-123',
      role: 'Scorekeeper',
    };
    expect(request).toBeDefined();
  });

  it('should export UpdateTournamentAdminRoleRequest from main index', () => {
    const request: UpdateTournamentAdminRoleRequest = {
      role: 'Admin',
    };
    expect(request).toBeDefined();
  });

  it('should export TransferOwnershipRequest from main index', () => {
    const request: TransferOwnershipRequest = {
      newOwnerUserId: 'new-owner-123',
    };
    expect(request).toBeDefined();
  });

  it('should export TournamentAdminRole from main index', () => {
    const role: TournamentAdminRole = 'Owner';
    expect(role).toBe('Owner');
  });
});
