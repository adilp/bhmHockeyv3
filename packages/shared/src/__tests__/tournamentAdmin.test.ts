import {
  TournamentAdminRole,
  TournamentAdminDto,
  AddTournamentAdminRequest,
  UpdateTournamentAdminRoleRequest,
  TransferOwnershipRequest,
} from '../types/tournamentAdmin';

describe('Tournament Admin Types', () => {
  it('should compile with valid TournamentAdminDto', () => {
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
    expect(admin.role).toBe('Admin');
  });

  it('should compile with valid AddTournamentAdminRequest', () => {
    const request: AddTournamentAdminRequest = {
      userId: 'user-123',
      role: 'Scorekeeper',
    };
    expect(request.role).toBe('Scorekeeper');
  });

  it('should compile with valid UpdateTournamentAdminRoleRequest', () => {
    const request: UpdateTournamentAdminRoleRequest = {
      role: 'Admin',
    };
    expect(request.role).toBe('Admin');
  });

  it('should compile with valid TransferOwnershipRequest', () => {
    const request: TransferOwnershipRequest = {
      newOwnerUserId: 'new-owner-123',
    };
    expect(request.newOwnerUserId).toBe('new-owner-123');
  });

  it('should support all TournamentAdminRole values', () => {
    const roles: TournamentAdminRole[] = ['Owner', 'Admin', 'Scorekeeper'];
    expect(roles).toHaveLength(3);
  });

  it('should allow null values for addedByUserId and addedByName', () => {
    const admin: TournamentAdminDto = {
      id: 'admin-123',
      tournamentId: 'tournament-456',
      userId: 'user-789',
      userFirstName: 'John',
      userLastName: 'Doe',
      userEmail: 'john@example.com',
      role: 'Owner',
      addedAt: '2026-01-18T00:00:00Z',
      addedByUserId: null,
      addedByName: null,
    };
    expect(admin.addedByUserId).toBeNull();
    expect(admin.addedByName).toBeNull();
  });
});
