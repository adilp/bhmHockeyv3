import { create } from 'zustand';
import { tournamentService, userService } from '@bhmhockey/api-client';
import type {
  TournamentDto,
  CreateTournamentRequest,
  TournamentTeamDto,
  TournamentMatchDto,
  TournamentRegistrationDto,
  CreateTournamentRegistrationRequest,
  UpdateTournamentRegistrationRequest,
  TournamentRegistrationResultDto,
  TeamAssignmentResultDto,
  BulkCreateTeamsResponse,
  StandingsDto,
  TeamStandingDto,
  TiedGroupDto,
  UpcomingTournamentMatchDto,
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

  // Registration state
  registrations: TournamentRegistrationDto[];
  myRegistration: TournamentRegistrationDto | null;
  isRegistering: boolean;
  registrationError: string | null;

  // Standings state
  standings: TeamStandingDto[];
  playoffCutoff: number | null;
  tiedGroups: TiedGroupDto[] | null;

  // Upcoming matches state
  myUpcomingMatches: UpcomingTournamentMatchDto[];
  isFetchingUpcoming: boolean;

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

  // Registration actions
  fetchMyRegistration: (tournamentId: string) => Promise<void>;
  registerForTournament: (tournamentId: string, request: CreateTournamentRegistrationRequest) => Promise<TournamentRegistrationResultDto | null>;
  withdrawFromTournament: (tournamentId: string) => Promise<boolean>;
  updateRegistration: (tournamentId: string, request: UpdateTournamentRegistrationRequest) => Promise<boolean>;
  markPayment: (tournamentId: string) => Promise<boolean>;
  fetchAllRegistrations: (tournamentId: string) => Promise<void>;
  verifyPayment: (tournamentId: string, registrationId: string, verified: boolean) => Promise<boolean>;
  assignPlayerToTeam: (tournamentId: string, registrationId: string, teamId: string) => Promise<boolean>;
  autoAssignTeams: (tournamentId: string, balanceBySkillLevel?: boolean) => Promise<TeamAssignmentResultDto | null>;
  bulkCreateTeams: (tournamentId: string, count: number, namePrefix: string) => Promise<BulkCreateTeamsResponse | null>;
  clearRegistrations: () => void;

  // Standings actions
  fetchStandings: (tournamentId: string) => Promise<void>;
  clearStandings: () => void;

  // Upcoming matches actions
  fetchMyUpcomingMatches: () => Promise<void>;
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

  // Registration state
  registrations: [],
  myRegistration: null,
  isRegistering: false,
  registrationError: null,

  // Standings state
  standings: [],
  playoffCutoff: null,
  tiedGroups: null,

  // Upcoming matches state
  myUpcomingMatches: [],
  isFetchingUpcoming: false,

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

  // Fetch current user's registration for a tournament
  fetchMyRegistration: async (tournamentId: string) => {
    set({ registrationError: null });
    try {
      const registration = await tournamentService.getMyRegistration(tournamentId);
      set({ myRegistration: registration });
    } catch (error: any) {
      // 404 is expected if user hasn't registered yet
      if (error?.response?.status === 404) {
        set({ myRegistration: null });
      } else {
        console.error('Failed to fetch registration:', error);
        set({
          registrationError: error instanceof Error ? error.message : 'Failed to load registration',
          myRegistration: null,
        });
      }
    }
  },

  // Register for tournament
  registerForTournament: async (tournamentId: string, request: CreateTournamentRegistrationRequest) => {
    set({ isRegistering: true, registrationError: null });
    try {
      const result = await tournamentService.registerForTournament(tournamentId, request);
      // Refresh registration to get full details
      await get().fetchMyRegistration(tournamentId);
      set({ isRegistering: false });
      return result;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to register for tournament';
      set({
        registrationError: errorMessage,
        isRegistering: false,
      });
      return null;
    }
  },

  // Withdraw from tournament
  withdrawFromTournament: async (tournamentId: string) => {
    set({ isRegistering: true, registrationError: null });
    try {
      await tournamentService.withdrawRegistration(tournamentId);
      set({ myRegistration: null, isRegistering: false });
      return true;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to withdraw from tournament';
      set({
        registrationError: errorMessage,
        isRegistering: false,
      });
      return false;
    }
  },

  // Update registration
  updateRegistration: async (tournamentId: string, request: UpdateTournamentRegistrationRequest) => {
    set({ isRegistering: true, registrationError: null });
    try {
      const updatedRegistration = await tournamentService.updateMyRegistration(tournamentId, request);
      set({ myRegistration: updatedRegistration, isRegistering: false });
      return true;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to update registration';
      set({
        registrationError: errorMessage,
        isRegistering: false,
      });
      return false;
    }
  },

  // Mark payment as sent
  markPayment: async (tournamentId: string) => {
    set({ isRegistering: true, registrationError: null });
    try {
      await tournamentService.markPayment(tournamentId);
      // Refresh registration to get updated payment status
      await get().fetchMyRegistration(tournamentId);
      set({ isRegistering: false });
      return true;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to mark payment';
      set({
        registrationError: errorMessage,
        isRegistering: false,
      });
      return false;
    }
  },

  // Admin: Fetch all registrations for a tournament
  fetchAllRegistrations: async (tournamentId: string) => {
    set({ isLoading: true, registrationError: null });
    try {
      const registrations = await tournamentService.getAllRegistrations(tournamentId);
      // Sort by registration date (earliest first)
      const sortedRegistrations = registrations.sort(
        (a, b) => new Date(a.registeredAt).getTime() - new Date(b.registeredAt).getTime()
      );
      set({ registrations: sortedRegistrations, isLoading: false });
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to load registrations';
      set({
        registrationError: errorMessage,
        isLoading: false,
      });
    }
  },

  // Admin: Verify payment
  verifyPayment: async (tournamentId: string, registrationId: string, verified: boolean) => {
    set({ registrationError: null });
    try {
      await tournamentService.verifyPayment(tournamentId, registrationId, { verified });

      // Update registration in the array
      const { registrations } = get();
      const updatedRegistrations = registrations.map(reg => {
        if (reg.id === registrationId) {
          return {
            ...reg,
            paymentStatus: verified ? ('Verified' as const) : ('Pending' as const),
            paymentVerifiedAt: verified ? new Date().toISOString() : undefined,
          };
        }
        return reg;
      });
      set({ registrations: updatedRegistrations });
      return true;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to verify payment';
      set({ registrationError: errorMessage });
      return false;
    }
  },

  // Admin: Assign player to team
  assignPlayerToTeam: async (tournamentId: string, registrationId: string, teamId: string) => {
    set({ registrationError: null });
    try {
      await tournamentService.assignPlayerToTeam(tournamentId, registrationId, { teamId });

      // Update registration in the array
      const { registrations, teams } = get();
      const team = teams.find(t => t.id === teamId);
      const updatedRegistrations = registrations.map(reg => {
        if (reg.id === registrationId) {
          return {
            ...reg,
            assignedTeamId: teamId,
            assignedTeamName: team?.name,
          };
        }
        return reg;
      });
      set({ registrations: updatedRegistrations });
      return true;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to assign player to team';
      set({ registrationError: errorMessage });
      return false;
    }
  },

  // Admin: Auto-assign all unassigned players to teams
  autoAssignTeams: async (tournamentId: string, balanceBySkillLevel?: boolean) => {
    const { processingId } = get();

    // Prevent double-clicks
    if (processingId === `auto-assign-${tournamentId}`) {
      return null;
    }

    set({ processingId: `auto-assign-${tournamentId}`, registrationError: null });
    try {
      const result = await tournamentService.autoAssignTeams(tournamentId, { balanceBySkillLevel });

      // Refresh registrations and teams after auto-assignment
      await Promise.all([
        get().fetchAllRegistrations(tournamentId),
        get().fetchTeams(tournamentId),
      ]);

      set({ processingId: null });
      return result;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to auto-assign teams';
      set({
        processingId: null,
        registrationError: errorMessage,
      });
      return null;
    }
  },

  // Admin: Bulk create teams
  bulkCreateTeams: async (tournamentId: string, count: number, namePrefix: string) => {
    set({ isCreating: true, registrationError: null });
    try {
      const result = await tournamentService.bulkCreateTeams(tournamentId, { count, namePrefix });

      // Refresh teams after creation
      await get().fetchTeams(tournamentId);

      set({ isCreating: false });
      return result;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to create teams';
      set({
        isCreating: false,
        registrationError: errorMessage,
      });
      return null;
    }
  },

  // Clear registration state when leaving screen
  clearRegistrations: () => set({
    registrations: [],
    myRegistration: null,
    registrationError: null,
  }),

  // Fetch standings for a tournament
  fetchStandings: async (tournamentId: string) => {
    set({ isLoading: true, error: null });
    try {
      const data = await tournamentService.getStandings(tournamentId);
      set({
        standings: data.standings,
        playoffCutoff: data.playoffCutoff ?? null,
        tiedGroups: data.tiedGroups ?? null,
        isLoading: false,
      });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to load standings';
      set({ error: message, isLoading: false });
    }
  },

  // Clear standings state
  clearStandings: () => set({
    standings: [],
    playoffCutoff: null,
    tiedGroups: null,
  }),

  // Fetch user's upcoming tournament matches
  fetchMyUpcomingMatches: async () => {
    set({ isFetchingUpcoming: true });
    try {
      const matches = await userService.getMyUpcomingTournamentMatches();
      // Sort by scheduledTime (nulls last)
      const sorted = matches.sort((a, b) => {
        if (!a.scheduledTime && !b.scheduledTime) return 0;
        if (!a.scheduledTime) return 1;
        if (!b.scheduledTime) return -1;
        return new Date(a.scheduledTime).getTime() - new Date(b.scheduledTime).getTime();
      });
      set({ myUpcomingMatches: sorted, isFetchingUpcoming: false });
    } catch (error) {
      console.error('Failed to fetch upcoming matches:', error);
      set({ myUpcomingMatches: [], isFetchingUpcoming: false });
    }
  },
}));
