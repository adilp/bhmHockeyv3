import { create } from 'zustand';
import { tournamentService } from '@bhmhockey/api-client';
import type {
  TournamentTeamDto,
  TournamentTeamMemberDto,
  PendingTeamInvitationDto,
  CreateTournamentTeamRequest,
  RespondToTeamInviteRequest,
  UserSearchResultDto,
} from '@bhmhockey/shared';

interface TournamentTeamState {
  // State
  currentTeam: TournamentTeamDto | null;
  teamMembers: TournamentTeamMemberDto[];
  myInvitations: PendingTeamInvitationDto[];
  isLoading: boolean;
  isProcessing: boolean;
  error: string | null;

  // Actions
  fetchTeamById: (tournamentId: string, teamId: string) => Promise<void>;
  fetchTeamMembers: (tournamentId: string, teamId: string) => Promise<void>;
  fetchMyInvitations: () => Promise<void>;
  createTeam: (tournamentId: string, name: string) => Promise<TournamentTeamDto | null>;
  addPlayer: (tournamentId: string, teamId: string, userId: string) => Promise<boolean>;
  removePlayer: (tournamentId: string, teamId: string, userId: string) => Promise<boolean>;
  respondToInvite: (
    tournamentId: string,
    teamId: string,
    accept: boolean,
    position?: string,
    customResponses?: string
  ) => Promise<boolean>;
  transferCaptain: (tournamentId: string, teamId: string, newCaptainUserId: string) => Promise<boolean>;
  searchUsers: (tournamentId: string, teamId: string, query: string) => Promise<UserSearchResultDto[]>;
  clearTeam: () => void;
  clearError: () => void;
}

export const useTournamentTeamStore = create<TournamentTeamState>((set, get) => ({
  // Initial state
  currentTeam: null,
  teamMembers: [],
  myInvitations: [],
  isLoading: false,
  isProcessing: false,
  error: null,

  // Fetch team by ID
  fetchTeamById: async (tournamentId: string, teamId: string) => {
    set({ isLoading: true, error: null });
    try {
      const team = await tournamentService.getTeamById(tournamentId, teamId);
      set({ currentTeam: team, isLoading: false });
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to load team',
        isLoading: false,
      });
    }
  },

  // Fetch team members
  fetchTeamMembers: async (tournamentId: string, teamId: string) => {
    set({ isLoading: true, error: null });
    try {
      const members = await tournamentService.getTeamMembers(tournamentId, teamId);
      // Sort by role (Captain first) then by name
      const sortedMembers = members.sort((a, b) => {
        if (a.role === 'Captain' && b.role !== 'Captain') return -1;
        if (a.role !== 'Captain' && b.role === 'Captain') return 1;
        return a.userFirstName.localeCompare(b.userFirstName);
      });
      set({ teamMembers: sortedMembers, isLoading: false });
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to load team members',
        isLoading: false,
      });
    }
  },

  // Fetch current user's pending invitations
  fetchMyInvitations: async () => {
    set({ error: null });
    try {
      const invitations = await tournamentService.getMyPendingInvitations();
      // Sort by invitation date (newest first)
      const sortedInvitations = invitations.sort(
        (a, b) => new Date(b.invitedAt).getTime() - new Date(a.invitedAt).getTime()
      );
      set({ myInvitations: sortedInvitations });
    } catch (error) {
      // Silently fail - user might not have any invitations
      console.log('Failed to fetch invitations:', error);
    }
  },

  // Create a new team
  createTeam: async (tournamentId: string, name: string) => {
    set({ isProcessing: true, error: null });
    try {
      const request: CreateTournamentTeamRequest = { name };
      const newTeam = await tournamentService.createTeam(tournamentId, request);
      set({ currentTeam: newTeam, isProcessing: false });
      return newTeam;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to create team';
      set({
        error: errorMessage,
        isProcessing: false,
      });
      return null;
    }
  },

  // Add a player to the team
  addPlayer: async (tournamentId: string, teamId: string, userId: string) => {
    set({ isProcessing: true, error: null });
    try {
      await tournamentService.addTeamMember(tournamentId, teamId, userId);
      // Refresh team members after adding
      await get().fetchTeamMembers(tournamentId, teamId);
      set({ isProcessing: false });
      return true;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to add player';
      set({
        error: errorMessage,
        isProcessing: false,
      });
      return false;
    }
  },

  // Remove a player from the team
  removePlayer: async (tournamentId: string, teamId: string, userId: string) => {
    set({ isProcessing: true, error: null });
    try {
      await tournamentService.removeTeamMember(tournamentId, teamId, userId);
      // Update team members list locally
      const { teamMembers } = get();
      const updatedMembers = teamMembers.filter(member => member.userId !== userId);
      set({ teamMembers: updatedMembers, isProcessing: false });
      return true;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to remove player';
      set({
        error: errorMessage,
        isProcessing: false,
      });
      return false;
    }
  },

  // Respond to a team invitation
  respondToInvite: async (
    tournamentId: string,
    teamId: string,
    accept: boolean,
    position?: string,
    customResponses?: string
  ) => {
    set({ isProcessing: true, error: null });
    try {
      const request: RespondToTeamInviteRequest = {
        accept,
        position,
        customResponses,
      };
      await tournamentService.respondToTeamInvite(tournamentId, teamId, request);

      // Remove the invitation from the list
      const { myInvitations } = get();
      const updatedInvitations = myInvitations.filter(
        inv => !(inv.tournamentId === tournamentId && inv.teamId === teamId)
      );
      set({ myInvitations: updatedInvitations, isProcessing: false });
      return true;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to respond to invitation';
      set({
        error: errorMessage,
        isProcessing: false,
      });
      return false;
    }
  },

  // Transfer captaincy to another team member
  transferCaptain: async (tournamentId: string, teamId: string, newCaptainUserId: string) => {
    set({ isProcessing: true, error: null });
    try {
      await tournamentService.transferCaptain(tournamentId, teamId, { newCaptainUserId });
      // Refresh team and members after transfer
      await Promise.all([
        get().fetchTeamById(tournamentId, teamId),
        get().fetchTeamMembers(tournamentId, teamId),
      ]);
      set({ isProcessing: false });
      return true;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to transfer captaincy';
      set({
        error: errorMessage,
        isProcessing: false,
      });
      return false;
    }
  },

  // Search users for adding to team
  searchUsers: async (tournamentId: string, teamId: string, query: string) => {
    try {
      const results = await tournamentService.searchUsers(tournamentId, teamId, query);
      return results;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to search users';
      set({ error: errorMessage });
      return [];
    }
  },

  // Clear current team data (for cleanup on navigation)
  clearTeam: () => set({
    currentTeam: null,
    teamMembers: [],
  }),

  // Clear error state
  clearError: () => set({ error: null }),
}));
