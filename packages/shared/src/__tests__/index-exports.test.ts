// Test that tournament admin types are properly exported from main index
import {
  TournamentAdminRole,
  TournamentAdminDto,
  AddTournamentAdminRequest,
  UpdateTournamentAdminRoleRequest,
  TransferOwnershipRequest,
  AutoRosterMember,
  AddAutoRosterMemberRequest,
  ReorderAutoRosterRequest,
  CreateEventRequest,
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

describe('Auto-Roster Type Exports', () => {
  it('should export AutoRosterMember from main index', () => {
    const member: AutoRosterMember = {
      id: 'member-123',
      userId: 'user-456',
      firstName: 'John',
      lastName: 'Doe',
      positions: { skater: 'Silver' },
      position: 'Skater',
      sortOrder: 0,
      addedAt: '2026-07-16T00:00:00Z',
    };
    expect(member).toBeDefined();
  });

  it('should export AddAutoRosterMemberRequest from main index', () => {
    const request: AddAutoRosterMemberRequest = {
      userId: 'user-123',
      position: 'Goalie',
    };
    expect(request).toBeDefined();
  });

  it('should export ReorderAutoRosterRequest from main index', () => {
    const request: ReorderAutoRosterRequest = {
      orderedUserIds: ['user-1', 'user-2'],
    };
    expect(request).toBeDefined();
  });

  it('should accept applyAutoRoster on CreateEventRequest', () => {
    const request: CreateEventRequest = {
      eventDate: '2026-07-23T00:00:00Z',
      maxPlayers: 12,
      cost: 0,
      organizationId: 'org-1',
      applyAutoRoster: false,
    };
    expect(request.applyAutoRoster).toBe(false);
  });
});
