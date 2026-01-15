import { create } from 'zustand';
import { tournamentService } from '@bhmhockey/api-client';
import type {
  TournamentDto,
  CreateTournamentRequest,
  TournamentTeamDto,
  TournamentMatchDto,
} from '@bhmhockey/shared';

interface TournamentState {
  // State
  tournaments: TournamentDto[];
  currentTournament: TournamentDto | null;
  teams: TournamentTeamDto[];
  matches: TournamentMatchDto[];
  isLoading: boolean;
  isCreating: boolean;
  processingId: string | null; // Track operations in progress
  error: string | null;

  // Actions
  fetchTournaments: () => Promise<void>;
  fetchTournamentById: (id: string) => Promise<void>;
  fetchTeams: (tournamentId: string) => Promise<void>;
  fetchMatches: (tournamentId: string) => Promise<void>;
  createTournament: (data: CreateTournamentRequest) => Promise<TournamentDto | null>;
  enterScore: (tournamentId: string, matchId: string, homeScore: number, awayScore: number) => Promise<boolean>;
  generateBracket: (tournamentId: string) => Promise<boolean>;
  clearTournament: () => void;
  clearError: () => void;
}

export const useTournamentStore = create<TournamentState>((set, get) => ({
  // Initial state
  tournaments: [],
  currentTournament: null,
  teams: [],
  matches: [],
  isLoading: false,
  isCreating: false,
  processingId: null,
  error: null,

  // Fetch all tournaments
  fetchTournaments: async () => {
    set({ isLoading: true, error: null });
    try {
      const tournaments = await tournamentService.getAll();
      // Sort by startDate ascending (earliest first)
      const sortedTournaments = tournaments.sort(
        (a, b) => new Date(a.startDate).getTime() - new Date(b.startDate).getTime()
      );
      set({ tournaments: sortedTournaments, isLoading: false });
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to load tournaments',
        isLoading: false,
      });
    }
  },

  // Fetch single tournament by ID, also fetches teams and matches
  fetchTournamentById: async (id: string) => {
    set({ isLoading: true, error: null });
    try {
      const tournament = await tournamentService.getById(id);
      set({ currentTournament: tournament, isLoading: false });

      // Also fetch teams and matches for this tournament
      await Promise.all([
        get().fetchTeams(id),
        get().fetchMatches(id),
      ]);
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to load tournament',
        isLoading: false,
      });
    }
  },

  // Fetch teams for a tournament
  fetchTeams: async (tournamentId: string) => {
    try {
      const teams = await tournamentService.getTeams(tournamentId);
      // Sort by seed (teams without seed go last)
      const sortedTeams = teams.sort((a, b) => {
        if (a.seed === undefined || a.seed === null) return 1;
        if (b.seed === undefined || b.seed === null) return -1;
        return a.seed - b.seed;
      });
      set({ teams: sortedTeams });
    } catch (error) {
      // Silently fail - teams might not exist yet
      console.log('Failed to fetch teams:', error);
    }
  },

  // Fetch matches for a tournament
  fetchMatches: async (tournamentId: string) => {
    try {
      const matches = await tournamentService.getMatches(tournamentId);
      // Sort by round, then by matchNumber
      const sortedMatches = matches.sort((a, b) => {
        if (a.round !== b.round) {
          return a.round - b.round;
        }
        return a.matchNumber - b.matchNumber;
      });
      set({ matches: sortedMatches });
    } catch (error) {
      // Silently fail - matches might not exist yet (bracket not generated)
      console.log('Failed to fetch matches:', error);
    }
  },

  // Create a new tournament
  createTournament: async (data: CreateTournamentRequest) => {
    set({ isCreating: true, error: null });
    try {
      const newTournament = await tournamentService.create(data);
      const { tournaments } = get();
      // Add new tournament to list, sorted by startDate
      const updatedTournaments = [...tournaments, newTournament].sort(
        (a, b) => new Date(a.startDate).getTime() - new Date(b.startDate).getTime()
      );
      set({ tournaments: updatedTournaments, isCreating: false });
      return newTournament;
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to create tournament',
        isCreating: false,
      });
      return null;
    }
  },

  // Enter score for a match
  enterScore: async (tournamentId: string, matchId: string, homeScore: number, awayScore: number) => {
    const { processingId } = get();

    // Prevent double-clicks
    if (processingId === matchId) {
      return false;
    }

    set({ processingId: matchId, error: null });
    try {
      await tournamentService.enterScore(tournamentId, matchId, { homeScore, awayScore });
      // Refresh matches after score entry (bracket may have advanced)
      await get().fetchMatches(tournamentId);
      set({ processingId: null });
      return true;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to enter score';
      set({
        processingId: null,
        error: errorMessage,
      });
      return false;
    }
  },

  // Generate bracket for a tournament
  generateBracket: async (tournamentId: string) => {
    const { processingId } = get();

    // Prevent double-clicks
    if (processingId === tournamentId) {
      return false;
    }

    set({ processingId: tournamentId, error: null });
    try {
      const generatedMatches = await tournamentService.generateBracket(tournamentId);
      // Sort matches by round, then matchNumber
      const sortedMatches = generatedMatches.sort((a, b) => {
        if (a.round !== b.round) {
          return a.round - b.round;
        }
        return a.matchNumber - b.matchNumber;
      });
      set({ matches: sortedMatches, processingId: null });
      return true;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to generate bracket';
      set({
        processingId: null,
        error: errorMessage,
      });
      return false;
    }
  },

  // Clear current tournament data (for cleanup on navigation)
  clearTournament: () => set({
    currentTournament: null,
    teams: [],
    matches: [],
  }),

  // Clear error state
  clearError: () => set({ error: null }),
}));
