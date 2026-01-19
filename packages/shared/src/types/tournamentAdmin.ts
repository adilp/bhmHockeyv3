// ============================================
// Tournament Admin Types (TRN-028)
// ============================================

// Tournament admin roles
export type TournamentAdminRole = 'Owner' | 'Admin' | 'Scorekeeper';

// Tournament admin DTO - response for listing admins
export interface TournamentAdminDto {
  id: string;
  tournamentId: string;
  userId: string;
  userFirstName: string;
  userLastName: string;
  userEmail: string;
  role: TournamentAdminRole;
  addedAt: string;
  addedByUserId: string | null;
  addedByName: string | null;
}

// Request to add a tournament admin
export interface AddTournamentAdminRequest {
  userId: string;
  role: TournamentAdminRole;  // Cannot be 'Owner' - ownership is only transferable
}

// Request to update tournament admin role
export interface UpdateTournamentAdminRoleRequest {
  role: TournamentAdminRole;  // Cannot be 'Owner' - ownership is only transferable
}

// Request to transfer tournament ownership
export interface TransferOwnershipRequest {
  newOwnerUserId: string;
}
