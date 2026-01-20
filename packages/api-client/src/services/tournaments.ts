import type {
  TournamentDto,
  CreateTournamentRequest,
  UpdateTournamentRequest,
  TournamentTeamDto,
  CreateTournamentTeamRequest,
  UpdateTournamentTeamRequest,
  TournamentMatchDto,
  EnterScoreRequest,
  ForfeitMatchRequest,
  TournamentRegistrationDto,
  TournamentRegistrationResultDto,
  CreateTournamentRegistrationRequest,
  UpdateTournamentRegistrationRequest,
  VerifyTournamentPaymentRequest,
  AssignPlayerToTeamRequest,
  AutoAssignTeamsRequest,
  BulkCreateTeamsRequest,
  BulkCreateTeamsResponse,
  TeamAssignmentResultDto,
  TournamentTeamMemberDto,
  AddTeamMemberRequest,
  RespondToTeamInviteRequest,
  TransferCaptainRequest,
  TransferCaptainResponse,
  UserSearchResultDto,
  PendingTeamInvitationDto,
  StandingsDto,
  UpcomingTournamentMatchDto,
  TournamentAdminDto,
  AddTournamentAdminRequest,
  UpdateTournamentAdminRoleRequest,
  TransferOwnershipRequest,
  TournamentAuditLogDto,
  AuditLogListResponse,
  AuditLogFilter,
  ResolveTiesRequest,
  TournamentAnnouncementDto,
  CreateTournamentAnnouncementRequest,
  UpdateTournamentAnnouncementRequest,
} from '@bhmhockey/shared';
import { apiClient } from '../client';

/**
 * Tournament service - handles tournament CRUD, lifecycle, teams, matches, and brackets
 */
export const tournamentService = {
  // ============================================
  // Core CRUD
  // ============================================

  /**
   * Get all tournaments
   */
  async getAll(): Promise<TournamentDto[]> {
    const response = await apiClient.instance.get<TournamentDto[]>('/tournaments');
    return response.data;
  },

  /**
   * Get tournament by ID
   */
  async getById(id: string): Promise<TournamentDto> {
    const response = await apiClient.instance.get<TournamentDto>(`/tournaments/${id}`);
    return response.data;
  },

  /**
   * Create a new tournament
   */
  async create(request: CreateTournamentRequest): Promise<TournamentDto> {
    const response = await apiClient.instance.post<TournamentDto>('/tournaments', request);
    return response.data;
  },

  /**
   * Update a tournament
   */
  async update(id: string, request: UpdateTournamentRequest): Promise<TournamentDto> {
    const response = await apiClient.instance.put<TournamentDto>(`/tournaments/${id}`, request);
    return response.data;
  },

  /**
   * Delete a tournament
   */
  async delete(id: string): Promise<void> {
    await apiClient.instance.delete(`/tournaments/${id}`);
  },

  // ============================================
  // Lifecycle
  // ============================================

  /**
   * Publish a tournament (Draft -> Open)
   */
  async publish(id: string): Promise<TournamentDto> {
    const response = await apiClient.instance.post<TournamentDto>(`/tournaments/${id}/publish`);
    return response.data;
  },

  /**
   * Close registration (Open -> RegistrationClosed)
   */
  async closeRegistration(id: string): Promise<TournamentDto> {
    const response = await apiClient.instance.post<TournamentDto>(`/tournaments/${id}/close-registration`);
    return response.data;
  },

  /**
   * Start the tournament (RegistrationClosed -> InProgress)
   */
  async start(id: string): Promise<TournamentDto> {
    const response = await apiClient.instance.post<TournamentDto>(`/tournaments/${id}/start`);
    return response.data;
  },

  /**
   * Complete the tournament (InProgress -> Completed)
   */
  async complete(id: string): Promise<TournamentDto> {
    const response = await apiClient.instance.post<TournamentDto>(`/tournaments/${id}/complete`);
    return response.data;
  },

  /**
   * Cancel the tournament (any state -> Cancelled)
   */
  async cancel(id: string): Promise<TournamentDto> {
    const response = await apiClient.instance.post<TournamentDto>(`/tournaments/${id}/cancel`);
    return response.data;
  },

  // ============================================
  // Teams
  // ============================================

  /**
   * Get all teams for a tournament
   */
  async getTeams(tournamentId: string): Promise<TournamentTeamDto[]> {
    const response = await apiClient.instance.get<TournamentTeamDto[]>(`/tournaments/${tournamentId}/teams`);
    return response.data;
  },

  /**
   * Get a specific team by ID
   */
  async getTeamById(tournamentId: string, teamId: string): Promise<TournamentTeamDto> {
    const response = await apiClient.instance.get<TournamentTeamDto>(`/tournaments/${tournamentId}/teams/${teamId}`);
    return response.data;
  },

  /**
   * Create a new team for a tournament
   */
  async createTeam(tournamentId: string, request: CreateTournamentTeamRequest): Promise<TournamentTeamDto> {
    const response = await apiClient.instance.post<TournamentTeamDto>(`/tournaments/${tournamentId}/teams`, request);
    return response.data;
  },

  /**
   * Update a team
   */
  async updateTeam(tournamentId: string, teamId: string, request: UpdateTournamentTeamRequest): Promise<TournamentTeamDto> {
    const response = await apiClient.instance.put<TournamentTeamDto>(`/tournaments/${tournamentId}/teams/${teamId}`, request);
    return response.data;
  },

  /**
   * Delete a team
   */
  async deleteTeam(tournamentId: string, teamId: string): Promise<void> {
    await apiClient.instance.delete(`/tournaments/${tournamentId}/teams/${teamId}`);
  },

  // ============================================
  // Team Members
  // ============================================

  /**
   * Get all members of a tournament team
   */
  async getTeamMembers(tournamentId: string, teamId: string): Promise<TournamentTeamMemberDto[]> {
    const response = await apiClient.instance.get<TournamentTeamMemberDto[]>(
      `/tournaments/${tournamentId}/teams/${teamId}/members`
    );
    return response.data;
  },

  /**
   * Add a member to a tournament team
   */
  async addTeamMember(tournamentId: string, teamId: string, userId: string): Promise<TournamentTeamMemberDto> {
    const request: AddTeamMemberRequest = { userId };
    const response = await apiClient.instance.post<TournamentTeamMemberDto>(
      `/tournaments/${tournamentId}/teams/${teamId}/members`,
      request
    );
    return response.data;
  },

  /**
   * Remove a member from a tournament team
   */
  async removeTeamMember(tournamentId: string, teamId: string, userId: string): Promise<void> {
    await apiClient.instance.delete(`/tournaments/${tournamentId}/teams/${teamId}/members/${userId}`);
  },

  /**
   * Respond to a team invitation (accept or decline)
   */
  async respondToTeamInvite(
    tournamentId: string,
    teamId: string,
    request: RespondToTeamInviteRequest
  ): Promise<TournamentTeamMemberDto> {
    const response = await apiClient.instance.post<TournamentTeamMemberDto>(
      `/tournaments/${tournamentId}/teams/${teamId}/members/respond`,
      request
    );
    return response.data;
  },

  /**
   * Transfer captaincy to another team member
   */
  async transferCaptain(
    tournamentId: string,
    teamId: string,
    request: TransferCaptainRequest
  ): Promise<TransferCaptainResponse> {
    const response = await apiClient.instance.post<TransferCaptainResponse>(
      `/tournaments/${tournamentId}/teams/${teamId}/transfer-captain`,
      request
    );
    return response.data;
  },

  /**
   * Search users for adding to a team
   */
  async searchUsers(tournamentId: string, teamId: string, query: string): Promise<UserSearchResultDto[]> {
    const response = await apiClient.instance.post<UserSearchResultDto[]>(
      `/tournaments/${tournamentId}/teams/${teamId}/search-users`,
      null,
      { params: { query } }
    );
    return response.data;
  },

  /**
   * Get current user's pending team invitations
   */
  async getMyPendingInvitations(): Promise<PendingTeamInvitationDto[]> {
    const response = await apiClient.instance.get<PendingTeamInvitationDto[]>(
      '/users/me/tournament-invitations'
    );
    return response.data;
  },

  // ============================================
  // Matches
  // ============================================

  /**
   * Get all matches for a tournament
   */
  async getMatches(tournamentId: string): Promise<TournamentMatchDto[]> {
    const response = await apiClient.instance.get<TournamentMatchDto[]>(`/tournaments/${tournamentId}/matches`);
    return response.data;
  },

  /**
   * Get a specific match by ID
   */
  async getMatchById(tournamentId: string, matchId: string): Promise<TournamentMatchDto> {
    const response = await apiClient.instance.get<TournamentMatchDto>(`/tournaments/${tournamentId}/matches/${matchId}`);
    return response.data;
  },

  /**
   * Get tournament standings
   */
  async getStandings(tournamentId: string): Promise<StandingsDto> {
    const response = await apiClient.instance.get<StandingsDto>(`/tournaments/${tournamentId}/standings`);
    return response.data;
  },

  /**
   * Enter score for a match
   */
  async enterScore(tournamentId: string, matchId: string, request: EnterScoreRequest): Promise<TournamentMatchDto> {
    const response = await apiClient.instance.put<TournamentMatchDto>(
      `/tournaments/${tournamentId}/matches/${matchId}/score`,
      request
    );
    return response.data;
  },

  /**
   * Forfeit a match
   */
  async forfeitMatch(tournamentId: string, matchId: string, request: ForfeitMatchRequest): Promise<TournamentMatchDto> {
    const response = await apiClient.instance.post<TournamentMatchDto>(
      `/tournaments/${tournamentId}/matches/${matchId}/forfeit`,
      request
    );
    return response.data;
  },

  // ============================================
  // Bracket
  // ============================================

  /**
   * Generate bracket for the tournament
   */
  async generateBracket(tournamentId: string): Promise<TournamentMatchDto[]> {
    const response = await apiClient.instance.post<TournamentMatchDto[]>(`/tournaments/${tournamentId}/generate-bracket`);
    return response.data;
  },

  /**
   * Clear bracket (delete all matches)
   */
  async clearBracket(tournamentId: string): Promise<void> {
    await apiClient.instance.delete(`/tournaments/${tournamentId}/bracket`);
  },

  // ============================================
  // Registration
  // ============================================

  /**
   * Register for a tournament
   */
  async registerForTournament(
    tournamentId: string,
    request: CreateTournamentRegistrationRequest
  ): Promise<TournamentRegistrationResultDto> {
    const response = await apiClient.instance.post<TournamentRegistrationResultDto>(
      `/tournaments/${tournamentId}/register`,
      request
    );
    return response.data;
  },

  /**
   * Get current user's registration for a tournament
   */
  async getMyRegistration(tournamentId: string): Promise<TournamentRegistrationDto> {
    const response = await apiClient.instance.get<TournamentRegistrationDto>(
      `/tournaments/${tournamentId}/register`
    );
    return response.data;
  },

  /**
   * Update current user's registration
   */
  async updateMyRegistration(
    tournamentId: string,
    request: UpdateTournamentRegistrationRequest
  ): Promise<TournamentRegistrationDto> {
    const response = await apiClient.instance.put<TournamentRegistrationDto>(
      `/tournaments/${tournamentId}/register`,
      request
    );
    return response.data;
  },

  /**
   * Withdraw registration from a tournament
   */
  async withdrawRegistration(tournamentId: string): Promise<void> {
    await apiClient.instance.delete(`/tournaments/${tournamentId}/register`);
  },

  /**
   * Get all registrations for a tournament (admin only)
   */
  async getAllRegistrations(tournamentId: string): Promise<TournamentRegistrationDto[]> {
    const response = await apiClient.instance.get<TournamentRegistrationDto[]>(
      `/tournaments/${tournamentId}/registrations`
    );
    return response.data;
  },

  /**
   * Mark payment as submitted for current user's registration
   */
  async markPayment(tournamentId: string): Promise<boolean> {
    const response = await apiClient.instance.post<boolean>(
      `/tournaments/${tournamentId}/register/mark-payment`
    );
    return response.data;
  },

  /**
   * Verify payment for a registration (admin only)
   */
  async verifyPayment(
    tournamentId: string,
    registrationId: string,
    request: VerifyTournamentPaymentRequest
  ): Promise<TournamentRegistrationDto> {
    const response = await apiClient.instance.put<TournamentRegistrationDto>(
      `/tournaments/${tournamentId}/registrations/${registrationId}/payment`,
      request
    );
    return response.data;
  },

  // ============================================
  // Team Assignment
  // ============================================

  /**
   * Assign a player to a team (admin only)
   */
  async assignPlayerToTeam(
    tournamentId: string,
    registrationId: string,
    request: AssignPlayerToTeamRequest
  ): Promise<TournamentRegistrationDto> {
    const response = await apiClient.instance.put<TournamentRegistrationDto>(
      `/tournaments/${tournamentId}/registrations/${registrationId}/team`,
      request
    );
    return response.data;
  },

  /**
   * Auto-assign players to teams based on skill level (admin only)
   */
  async autoAssignTeams(
    tournamentId: string,
    request: AutoAssignTeamsRequest
  ): Promise<TeamAssignmentResultDto> {
    const response = await apiClient.instance.post<TeamAssignmentResultDto>(
      `/tournaments/${tournamentId}/assign-teams`,
      request
    );
    return response.data;
  },

  /**
   * Bulk create teams for a tournament (admin only)
   */
  async bulkCreateTeams(
    tournamentId: string,
    request: BulkCreateTeamsRequest
  ): Promise<BulkCreateTeamsResponse> {
    const response = await apiClient.instance.post<BulkCreateTeamsResponse>(
      `/tournaments/${tournamentId}/create-teams`,
      request
    );
    return response.data;
  },

  // ============================================
  // User Matches
  // ============================================

  /**
   * Get current user's upcoming tournament matches across all tournaments
   */
  async getMyUpcomingMatches(): Promise<UpcomingTournamentMatchDto[]> {
    const response = await apiClient.instance.get<UpcomingTournamentMatchDto[]>(
      '/users/me/upcoming-tournament-matches'
    );
    return response.data;
  },

  // ============================================
  // Admin Management
  // ============================================

  /**
   * Get all admins for a tournament
   */
  async getAdmins(tournamentId: string): Promise<TournamentAdminDto[]> {
    const response = await apiClient.instance.get<TournamentAdminDto[]>(
      `/tournaments/${tournamentId}/admins`
    );
    return response.data;
  },

  /**
   * Add a new admin to a tournament (Owner only)
   */
  async addAdmin(
    tournamentId: string,
    request: AddTournamentAdminRequest
  ): Promise<TournamentAdminDto> {
    const response = await apiClient.instance.post<TournamentAdminDto>(
      `/tournaments/${tournamentId}/admins`,
      request
    );
    return response.data;
  },

  /**
   * Update an admin's role (Owner only)
   */
  async updateAdminRole(
    tournamentId: string,
    userId: string,
    request: UpdateTournamentAdminRoleRequest
  ): Promise<TournamentAdminDto> {
    const response = await apiClient.instance.put<TournamentAdminDto>(
      `/tournaments/${tournamentId}/admins/${userId}`,
      request
    );
    return response.data;
  },

  /**
   * Remove an admin from a tournament (Owner only)
   */
  async removeAdmin(tournamentId: string, userId: string): Promise<void> {
    await apiClient.instance.delete(`/tournaments/${tournamentId}/admins/${userId}`);
  },

  /**
   * Transfer tournament ownership to another admin (Owner only)
   */
  async transferOwnership(
    tournamentId: string,
    request: TransferOwnershipRequest
  ): Promise<void> {
    await apiClient.instance.post(
      `/tournaments/${tournamentId}/transfer-ownership`,
      request
    );
  },

  // ============================================
  // Audit Log
  // ============================================

  /**
   * Get audit log entries for a tournament (Scorekeeper+ access)
   */
  async getAuditLogs(
    tournamentId: string,
    offset: number = 0,
    limit: number = 20,
    filter?: AuditLogFilter
  ): Promise<AuditLogListResponse> {
    const params: Record<string, string | number> = { offset, limit };
    if (filter?.action) params.action = filter.action;
    if (filter?.fromDate) params.fromDate = filter.fromDate;
    if (filter?.toDate) params.toDate = filter.toDate;

    const response = await apiClient.instance.get<AuditLogListResponse>(
      `/tournaments/${tournamentId}/audit-log`,
      { params }
    );
    return response.data;
  },

  // ============================================
  // Standings / Tie Resolution
  // ============================================

  /**
   * Manually resolve ties in standings (Admin+ only)
   */
  async resolveTies(
    tournamentId: string,
    request: ResolveTiesRequest
  ): Promise<void> {
    await apiClient.instance.put(
      `/tournaments/${tournamentId}/standings/resolve`,
      request
    );
  },

  // ============================================
  // Announcements
  // ============================================

  /**
   * Get announcements for a tournament (filtered by user visibility)
   */
  async getAnnouncements(tournamentId: string): Promise<TournamentAnnouncementDto[]> {
    const response = await apiClient.instance.get<TournamentAnnouncementDto[]>(
      `/tournaments/${tournamentId}/announcements`
    );
    return response.data;
  },

  /**
   * Create a new announcement (Admin only)
   */
  async createAnnouncement(
    tournamentId: string,
    request: CreateTournamentAnnouncementRequest
  ): Promise<TournamentAnnouncementDto> {
    const response = await apiClient.instance.post<TournamentAnnouncementDto>(
      `/tournaments/${tournamentId}/announcements`,
      request
    );
    return response.data;
  },

  /**
   * Update an announcement (Admin only)
   */
  async updateAnnouncement(
    tournamentId: string,
    announcementId: string,
    request: UpdateTournamentAnnouncementRequest
  ): Promise<TournamentAnnouncementDto> {
    const response = await apiClient.instance.put<TournamentAnnouncementDto>(
      `/tournaments/${tournamentId}/announcements/${announcementId}`,
      request
    );
    return response.data;
  },

  /**
   * Delete an announcement (Admin only)
   */
  async deleteAnnouncement(tournamentId: string, announcementId: string): Promise<void> {
    await apiClient.instance.delete(`/tournaments/${tournamentId}/announcements/${announcementId}`);
  },
};
