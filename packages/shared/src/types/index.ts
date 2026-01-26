// User types
export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  positions?: UserPositions;  // Multi-position support
  venmoHandle?: string;
  role: UserRole;
  pushToken?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  // Badge fields (optional - included in roster responses)
  badges?: UserBadgeDto[];    // Top 3 badges by displayOrder
  totalBadgeCount?: number;   // Total badges user has earned
}

export type UserRole = 'Player' | 'Organizer' | 'Admin';

// Simplified positions: Goalie or Skater only
export type Position = 'Goalie' | 'Skater';

// Position-skill mapping for multi-position support
export type UserPositions = {
  goalie?: SkillLevel;
  skater?: SkillLevel;
};

// Organization types
export interface Organization {
  id: string;
  name: string;
  description?: string;
  creatorId: string;
  location?: string;
  skillLevels?: SkillLevel[];  // Multiple skill levels allowed (e.g., ["Silver", "Gold"])
  subscriberCount: number;
  isSubscribed: boolean;
  isAdmin: boolean;       // True if current user is an admin of this organization
  createdAt: string;
  // Event defaults
  defaultDayOfWeek?: number | null;      // 0=Sunday, 6=Saturday
  defaultStartTime?: string | null;       // "HH:mm:ss" format
  defaultDurationMinutes?: number | null;
  defaultMaxPlayers?: number | null;
  defaultCost?: number | null;
  defaultVenue?: string | null;
  defaultVisibility?: EventVisibility | null;
}

// Organization admin info
export interface OrganizationAdmin {
  id: string;
  userId: string;
  firstName: string;
  lastName: string;
  email: string;
  addedAt: string;
  addedByUserId?: string;
  addedByName?: string;
}

export interface AddAdminRequest {
  userId: string;
}

// Member/subscriber info - visible to all subscribers
// Email is only populated for admin viewers
export interface OrganizationMember {
  id: string;
  firstName: string;
  lastName: string;
  email: string | null;  // Only populated for admin viewers
  positions?: UserPositions;  // Multi-position support
  subscribedAt: string;
  isAdmin: boolean;  // True if this member is an admin of the organization
  badges?: UserBadgeDto[];  // Top 3 badges by displayOrder
  totalBadgeCount?: number;  // Total badges user has earned
}

export type SkillLevel = 'Gold' | 'Silver' | 'Bronze' | 'D-League';

export interface OrganizationSubscription {
  id: string;
  organization: Organization;
  notificationEnabled: boolean;
  subscribedAt: string;
}

export interface CreateOrganizationRequest {
  name: string;
  description?: string;
  location?: string;
  skillLevels?: SkillLevel[];  // Multiple skill levels allowed
  // Event defaults
  defaultDayOfWeek?: number | null;      // 0=Sunday, 6=Saturday
  defaultStartTime?: string | null;       // "HH:mm:ss" format
  defaultDurationMinutes?: number | null;
  defaultMaxPlayers?: number | null;
  defaultCost?: number | null;
  defaultVenue?: string | null;
  defaultVisibility?: EventVisibility | null;
}

export interface UpdateOrganizationRequest {
  name?: string;
  description?: string;
  location?: string;
  skillLevels?: SkillLevel[];  // Multiple skill levels allowed
  // Event defaults
  defaultDayOfWeek?: number | null;      // 0=Sunday, 6=Saturday
  defaultStartTime?: string | null;       // "HH:mm:ss" format
  defaultDurationMinutes?: number | null;
  defaultMaxPlayers?: number | null;
  defaultCost?: number | null;
  defaultVenue?: string | null;
  defaultVisibility?: EventVisibility | null;
}

// Event types
export interface Event {
  id: string;
  organizationId?: string;      // Optional - null for standalone pickup games
  creatorId: string;
  name: string;
  description?: string;
  eventDate: string;
  duration: number; // minutes
  venue?: string;
  maxPlayers: number;
  cost: number;
  registrationDeadline?: string;
  status: EventStatus;
  visibility: EventVisibility;  // Controls who can see/register
  createdAt: string;
  updatedAt: string;
}

export type EventStatus = 'Draft' | 'Published' | 'Full' | 'Completed' | 'Cancelled';

// Visibility options:
// - Public: Anyone can see and register
// - OrganizationMembers: Only subscribers of the organization (requires organizationId)
// - InviteOnly: Only invited users can see/register (Phase B: will use invitations)
export type EventVisibility = 'Public' | 'OrganizationMembers' | 'InviteOnly';

export interface EventRegistration {
  id: string;
  eventId: string;
  userId: string;
  status: RegistrationStatus;
  registeredAt: string;
}

export type RegistrationStatus = 'Registered' | 'Waitlisted' | 'Cancelled';

// Payment status for event registrations (Phase 4)
export type PaymentStatus = 'Pending' | 'MarkedPaid' | 'Verified';

// Registration result for register endpoint (Phase 5 - includes waitlist info)
export interface RegistrationResultDto {
  status: 'Registered' | 'Waitlisted';
  waitlistPosition: number | null;
  message: string;
}

// Team assignment for events
export type TeamAssignment = 'Black' | 'White';

// EventDto - API response with computed fields
export interface EventDto {
  id: string;
  organizationId?: string;       // Optional - null for standalone events
  organizationName?: string;     // Optional - null for standalone events
  creatorId: string;             // Who created the event
  name?: string;                 // Optional - null if no custom name set
  description?: string;
  eventDate: string;
  duration: number;
  venue?: string;
  maxPlayers: number;
  registeredCount: number;
  cost: number;
  registrationDeadline?: string;
  status: EventStatus;
  visibility: EventVisibility;   // Public, OrganizationMembers, InviteOnly
  skillLevels?: SkillLevel[];    // Event's skill levels (overrides org if set)
  isRegistered: boolean;
  canManage: boolean;            // True if current user can manage this event (creator for standalone, org admin for org events)
  createdAt: string;
  // Roster draft mode
  isRosterPublished: boolean;    // True if roster/waitlist details are visible to players
  // Payment fields (Phase 4)
  creatorVenmoHandle?: string;   // For "Pay with Venmo" button
  myPaymentStatus?: PaymentStatus; // Current user's payment status
  // Team assignment
  myTeamAssignment?: TeamAssignment; // Current user's team ("Black" or "White")
  // Organizer fields (only populated when canManage = true)
  unpaidCount?: number;          // Count of registrations with PaymentStatus != "Verified"
  // Waitlist fields (Phase 5)
  waitlistCount: number;         // Number of people on waitlist
  myWaitlistPosition?: number;   // Current user's waitlist position (null if not waitlisted)
  myPaymentDeadline?: string;    // Current user's payment deadline after promotion (ISO date string)
  amIWaitlisted: boolean;        // Convenience flag - true if current user is on waitlist
}

// EventRegistrationDto - API response for registration with user details
export interface EventRegistrationDto {
  id: string;
  eventId: string;
  user: User;
  status: RegistrationStatus;
  registeredAt: string;
  // Position tracking
  registeredPosition?: Position;
  // Payment fields (Phase 4)
  paymentStatus?: PaymentStatus;
  paymentMarkedAt?: string;
  paymentVerifiedAt?: string;
  // Team assignment
  teamAssignment?: TeamAssignment;
  // Roster ordering
  rosterOrder?: number;          // Order within team (lower = higher on roster)
  // Waitlist fields (Phase 5)
  waitlistPosition?: number;     // Position in waitlist (1 = first, null = not waitlisted)
  promotedAt?: string;           // When user was promoted from waitlist (ISO date string)
  paymentDeadlineAt?: string;    // Deadline to pay after promotion (ISO date string)
  isWaitlisted: boolean;         // True if Status == "Waitlisted"
}

// Event request types
export interface CreateEventRequest {
  organizationId?: string;       // Optional - omit for standalone pickup games
  name?: string;                 // Optional - backend generates default if not provided
  description?: string;
  eventDate: string;
  duration?: number;             // Optional - backend uses default if not provided
  venue?: string;
  maxPlayers: number;
  cost: number;
  registrationDeadline?: string;
  visibility?: EventVisibility;  // Default: 'Public'
  skillLevels?: SkillLevel[];    // Optional - overrides org's skill levels if set
}

export interface UpdateEventRequest {
  name?: string;
  description?: string;
  eventDate?: string;
  duration?: number;
  venue?: string;
  maxPlayers?: number;
  cost?: number;
  registrationDeadline?: string;
  status?: EventStatus;
  visibility?: EventVisibility;  // Can change visibility after creation
  skillLevels?: SkillLevel[];    // Can change skill levels after creation
}

// Payment request types (Phase 4)
export interface MarkPaymentRequest {
  paymentReference?: string;
}

export interface UpdatePaymentStatusRequest {
  paymentStatus: 'Verified' | 'Pending';
}

// Payment update result DTO (Phase 5 - payment verification response)
export interface PaymentUpdateResultDto {
  success: boolean;
  promoted: boolean;
  message: string;
  registration?: EventRegistrationDto;
}

// Move operation result DTO (Phase 3 - roster/waitlist moves)
export interface MoveResultDto {
  success: boolean;
  message: string;
  registration?: EventRegistrationDto;
}

// Waitlist reorder types (Phase 5)
export interface WaitlistOrderItem {
  registrationId: string;
  position: number;
}

export interface ReorderWaitlistRequest {
  items: WaitlistOrderItem[];
}

// Team assignment request
export interface UpdateTeamAssignmentRequest {
  teamAssignment: TeamAssignment;
}

// Roster order types
export interface RosterOrderItem {
  registrationId: string;
  teamAssignment: TeamAssignment;
  rosterOrder: number;
}

export interface UpdateRosterOrderRequest {
  items: RosterOrderItem[];
}

// Auth types
export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  positions?: UserPositions;
  venmoHandle?: string;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  user: User;
}

export interface UpdateUserProfileRequest {
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  positions?: UserPositions;  // Multi-position support
  venmoHandle?: string;
}

// Registration request with optional position
export interface RegisterForEventRequest {
  position?: Position;  // Required if user has multiple positions
}

// API Response types
export interface ApiResponse<T> {
  data: T;
  success: boolean;
  message?: string;
}

export interface ApiError {
  message: string;
  errors?: Record<string, string[]>;
  statusCode: number;
}

// Notification types (Phase: In-App Notification Center)
export type NotificationType =
  | 'new_event'
  | 'waitlist_promoted'
  | 'waitlist_joined'
  | 'waitlist_promotion'
  | 'payment_reminder'
  | 'game_reminder'
  | 'organizer_payment_reminder';

export interface Notification {
  id: string;
  type: NotificationType;
  title: string;
  body: string;
  data?: Record<string, string>;
  organizationId?: string;
  eventId?: string;
  isRead: boolean;
  readAt?: string;
  createdAt: string;
}

export interface NotificationListResponse {
  notifications: Notification[];
  unreadCount: number;
  totalCount: number;
  hasMore: boolean;
}

export interface UnreadCountResponse {
  unreadCount: number;
}

// Badge types (Phase: Badge System)
export type BadgeCategory = 'achievement' | 'milestone' | 'social';

export interface BadgeTypeDto {
  id: string;
  code: string;
  name: string;
  description: string;
  iconName: string;
  category: BadgeCategory;
}

export interface UserBadgeDto {
  id: string;
  badgeType: BadgeTypeDto;
  context: Record<string, unknown>;
  earnedAt: string;
  displayOrder: number | null;
}

export interface UncelebratedBadgeDto {
  id: string;
  badgeType: BadgeTypeDto;
  context?: Record<string, unknown>;
  earnedAt: string;  // ISO date string
  totalAwarded: number;  // Rarity count
}

export interface UpdateBadgeOrderRequest {
  badgeIds: string[];
}

// Context types for specific badge types
export interface BadgeContextTournament {
  tournamentName: string;
  tournamentId?: string;
  year: number;
}

export interface BadgeContextGeneric {
  description: string;
}

// User summary with badges (for roster cards)
export interface UserSummaryDto {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  positions?: UserPositions;
  badges: UserBadgeDto[];     // Top 3 badges by displayOrder
  totalBadgeCount: number;    // Total badges user has earned
}

// ============================================
// Tournament Types (TRN-001)
// ============================================

// Tournament format options
export type TournamentFormat = 'SingleElimination' | 'DoubleElimination' | 'RoundRobin';

// Team formation options
export type TournamentTeamFormation = 'OrganizerAssigned' | 'PreFormed';

// Tournament status (lifecycle state machine)
export type TournamentStatus =
  | 'Draft'           // Created but not visible to public
  | 'Open'            // Registration open, visible to public
  | 'RegistrationClosed'  // Past deadline, preparing to start
  | 'InProgress'      // Tournament ongoing, games being played
  | 'Completed'       // All games finished, badges awarded
  | 'Postponed'       // Temporarily paused with new dates
  | 'Cancelled';      // Tournament cancelled

// Fee type options
export type TournamentFeeType = 'PerPlayer' | 'PerTeam';

// Tournament DTO - full tournament details
export interface TournamentDto {
  id: string;
  organizationId?: string;
  organizationName?: string;
  creatorId: string;

  // Basic info
  name: string;
  description?: string;

  // Format & Configuration
  format: TournamentFormat;
  teamFormation: TournamentTeamFormation;

  // Status
  status: TournamentStatus;

  // Dates
  startDate: string;
  endDate: string;
  registrationDeadline: string;
  postponedToDate?: string;

  // Team configuration
  maxTeams: number;
  minPlayersPerTeam?: number;
  maxPlayersPerTeam?: number;
  allowMultiTeam: boolean;
  allowSubstitutions: boolean;

  // Payment
  entryFee: number;
  feeType?: TournamentFeeType;

  // Round Robin config
  pointsWin: number;
  pointsTie: number;
  pointsLoss: number;
  playoffFormat?: TournamentFormat;
  playoffTeamsCount?: number;

  // Content
  rulesContent?: string;
  waiverUrl?: string;
  venue?: string;

  // Configuration (JSON strings)
  notificationSettings?: string;
  customQuestions?: string;
  eligibilityRequirements?: string;
  tiebreakerOrder?: string;

  // Timestamps
  createdAt: string;
  updatedAt: string;
  publishedAt?: string;
  startedAt?: string;
  completedAt?: string;
  cancelledAt?: string;

  // Computed fields
  canManage: boolean;
}

// Request to create a tournament
export interface CreateTournamentRequest {
  organizationId?: string;

  // Basic info
  name: string;
  description?: string;

  // Format & Configuration
  format: TournamentFormat;
  teamFormation: TournamentTeamFormation;

  // Dates
  startDate: string;
  endDate: string;
  registrationDeadline: string;

  // Team configuration
  maxTeams: number;
  minPlayersPerTeam?: number;
  maxPlayersPerTeam?: number;
  allowMultiTeam?: boolean;
  allowSubstitutions?: boolean;

  // Payment
  entryFee?: number;
  feeType?: TournamentFeeType;

  // Round Robin config
  pointsWin?: number;
  pointsTie?: number;
  pointsLoss?: number;
  playoffFormat?: TournamentFormat;
  playoffTeamsCount?: number;

  // Content
  rulesContent?: string;
  waiverUrl?: string;
  venue?: string;

  // Configuration (JSON strings)
  notificationSettings?: string;
  customQuestions?: string;
  eligibilityRequirements?: string;
  tiebreakerOrder?: string;
}

// Request to update a tournament (all fields optional for patch semantics)
export interface UpdateTournamentRequest {
  // Basic info
  name?: string;
  description?: string;

  // Format & Configuration
  format?: TournamentFormat;
  teamFormation?: TournamentTeamFormation;

  // Dates
  startDate?: string;
  endDate?: string;
  registrationDeadline?: string;

  // Team configuration
  maxTeams?: number;
  minPlayersPerTeam?: number;
  maxPlayersPerTeam?: number;
  allowMultiTeam?: boolean;
  allowSubstitutions?: boolean;

  // Payment
  entryFee?: number;
  feeType?: TournamentFeeType;

  // Round Robin config
  pointsWin?: number;
  pointsTie?: number;
  pointsLoss?: number;
  playoffFormat?: TournamentFormat;
  playoffTeamsCount?: number;

  // Content
  rulesContent?: string;
  waiverUrl?: string;
  venue?: string;

  // Configuration (JSON strings)
  notificationSettings?: string;
  customQuestions?: string;
  eligibilityRequirements?: string;
  tiebreakerOrder?: string;
}

// Import tournament admin types from dedicated file
export * from './tournamentAdmin';

// Import tournament announcement types from dedicated file
export * from './tournamentAnnouncement';

// ============================================
// Tournament Team Types (TRN-003)
// ============================================

// Tournament team status
export type TournamentTeamStatus = 'Registered' | 'Waitlisted' | 'Active' | 'Eliminated' | 'Winner';

// Tournament team DTO
export interface TournamentTeamDto {
  id: string;
  tournamentId: string;
  name: string;

  // Captain info
  captainUserId?: string;
  captainName?: string;

  // Status & Position
  status: TournamentTeamStatus;
  waitlistPosition?: number;
  seed?: number;
  finalPlacement?: number;
  hasBye: boolean;

  // Statistics
  wins: number;
  losses: number;
  ties: number;
  points: number;
  goalsFor: number;
  goalsAgainst: number;
  goalDifferential: number;

  // Payment
  paymentStatus?: PaymentStatus;

  // Timestamps
  createdAt: string;
  updatedAt: string;
}

export interface CreateTournamentTeamRequest {
  name: string;
  seed?: number;
}

export interface UpdateTournamentTeamRequest {
  name?: string;
  seed?: number;
}

// ============================================
// Tournament Match Types (TRN-003)
// ============================================

// Tournament match status
export type TournamentMatchStatus = 'Scheduled' | 'InProgress' | 'Completed' | 'Cancelled' | 'Forfeit' | 'Bye';

// Tournament match DTO
export interface TournamentMatchDto {
  id: string;
  tournamentId: string;

  // Teams
  homeTeamId?: string;
  homeTeamName?: string;
  awayTeamId?: string;
  awayTeamName?: string;

  // Match Info
  round: number;
  matchNumber: number;
  bracketPosition?: string;
  bracketType?: 'Winners' | 'Losers' | 'GrandFinal'; // For double elimination tournaments

  // Schedule & Venue
  isBye: boolean;
  scheduledTime?: string;
  venue?: string;

  // Status & Score
  status: TournamentMatchStatus;
  homeScore?: number;
  awayScore?: number;

  // Winner
  winnerTeamId?: string;
  winnerTeamName?: string;

  // Forfeit
  forfeitReason?: string;

  // Bracket Navigation
  nextMatchId?: string;
  loserNextMatchId?: string;

  // Timestamps
  createdAt: string;
  updatedAt: string;
}

// ============================================
// Tournament Match Request Types (TRN-005)
// ============================================

// Request to enter match scores
export interface EnterScoreRequest {
  homeScore: number;
  awayScore: number;
  overtimeWinnerId?: string;  // Required when tied in elimination formats
}

// Request to forfeit a match
export interface ForfeitMatchRequest {
  forfeitingTeamId: string;
  reason: string;
}

// ============================================
// Upcoming Tournament Match (TRN-032)
// ============================================

// Upcoming tournament match for home screen display
export interface UpcomingTournamentMatchDto {
  id: string;
  tournamentId: string;
  tournamentName: string;

  // User's team info
  userTeamId: string;
  userTeamName: string;

  // Opponent team info (may be undefined if TBD)
  opponentTeamId?: string;
  opponentTeamName?: string;

  // Match details
  round: number;
  matchNumber: number;
  bracketPosition?: string;
  status: 'Scheduled' | 'InProgress';

  // Schedule
  scheduledTime?: string;
  venue?: string;

  // Is the user's team home or away?
  isHomeTeam: boolean;
}

// ============================================
// Tournament Standings Types (TRN-021/TRN-022)
// ============================================

// Individual team standing in a tournament
export interface TeamStandingDto {
  teamId: string;
  teamName: string;
  rank: number;
  wins: number;
  losses: number;
  ties: number;
  points: number;
  goalsFor: number;
  goalsAgainst: number;
  goalDifferential: number;
  gamesPlayed: number;
  isPlayoffBound: boolean;
}

// Group of teams tied and cannot be automatically resolved
export interface TiedGroupDto {
  teamIds: string[];
  reason: string;
}

// Complete standings response for a tournament
export interface StandingsDto {
  standings: TeamStandingDto[];
  playoffCutoff?: number;
  tiedGroups?: TiedGroupDto[];
}

// ============================================
// Tournament Team Assignment Types (TRN-009)
// ============================================

// Request to assign a player to a specific team
export interface AssignPlayerToTeamRequest {
  teamId: string;
}

// Request for auto-assigning all unassigned players to teams
export interface AutoAssignTeamsRequest {
  balanceBySkillLevel?: boolean;
}

// Request to bulk create empty teams
export interface BulkCreateTeamsRequest {
  count: number;
  namePrefix: string;
}

// Response from bulk create teams
export interface BulkCreateTeamsResponse {
  teams: TournamentTeamDto[];
  message: string;
}

// Result of team assignment operations
export interface TeamAssignmentResultDto {
  success: boolean;
  message: string;
  assignedCount: number;
  unassignedCount: number;
}

// TRN-012: Captain Management
export type TournamentTeamMemberRole = 'Captain' | 'Player';
export type TournamentTeamMemberStatus = 'Pending' | 'Accepted' | 'Declined';

// Tournament team member - full member details
export interface TournamentTeamMemberDto {
  id: string;
  teamId: string;
  userId: string;
  userFirstName: string;
  userLastName: string;
  userEmail: string;
  role: TournamentTeamMemberRole;
  status: TournamentTeamMemberStatus;
  position?: string;  // 'Goalie' | 'Skater'
  joinedAt: string;
  respondedAt?: string;
  leftAt?: string;
}

// TRN-012: Captain Management - Request to transfer captaincy
export interface TransferCaptainRequest {
  newCaptainUserId: string;
}

// TRN-012: Captain Management - Response from transferring captaincy
export interface TransferCaptainResponse {
  success: boolean;
  message: string;
  previousCaptainId: string;
  newCaptainId: string;
}

// TRN-012: Team Members - Request to add a team member
export interface AddTeamMemberRequest {
  userId: string;
}

// TRN-012: Team Members - Request to respond to team invite
export interface RespondToTeamInviteRequest {
  accept: boolean;
  position?: string;  // Required if accept=true: "Goalie" or "Skater"
  customResponses?: string;  // JSON string
}

// TRN-012: Team Members - User search result for captain search
export interface UserSearchResultDto {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
}

// TRN-012: Team Members - Pending team invitation DTO
export interface PendingTeamInvitationDto {
  memberId: string;
  teamId: string;
  teamName: string;
  tournamentId: string;
  tournamentName: string;
  captainName: string;
  invitedAt: string;
}

// ============================================
// Tournament Registration Types (TRN-007)
// ============================================

// Tournament registration status
export type TournamentRegistrationStatus = 'Registered' | 'Waitlisted' | 'Cancelled';

// Tournament waiver status
export type TournamentWaiverStatus = 'Pending' | 'Accepted';

// Tournament registration DTO - full registration details with user info
export interface TournamentRegistrationDto {
  id: string;
  tournamentId: string;
  user: User;
  status: TournamentRegistrationStatus;
  position?: string;
  waitlistPosition?: number;
  promotedAt?: string;
  isWaitlisted: boolean;
  assignedTeamId?: string;
  assignedTeamName?: string;
  customResponses?: string;
  waiverStatus?: TournamentWaiverStatus;
  paymentStatus?: PaymentStatus;
  paymentMarkedAt?: string;
  paymentVerifiedAt?: string;
  paymentDeadlineAt?: string;
  registeredAt: string;
  updatedAt: string;
  cancelledAt?: string;
}

// Request to register for a tournament
export interface CreateTournamentRegistrationRequest {
  position: string;
  customResponses?: string;
  waiverAccepted: boolean;
}

// Request to update a tournament registration
export interface UpdateTournamentRegistrationRequest {
  position?: string;
  customResponses?: string;
}

// Result of tournament registration operation
export interface TournamentRegistrationResultDto {
  status: TournamentRegistrationStatus;
  waitlistPosition?: number;
  message: string;
}

// Request to verify tournament payment
export interface VerifyTournamentPaymentRequest {
  verified: boolean;
}

// ============================================
// Tournament History Types
// ============================================

// User tournament summary for history/list views
export interface UserTournamentSummaryDto {
  id: string;
  name: string;
  status: TournamentStatus;
  startDate: string;
  endDate: string;
  completedAt?: string;

  // User's participation info
  teamId?: string;
  teamName?: string;
  finalPlacement?: number;
  userRole?: 'Player' | 'Captain' | 'Admin' | 'Owner' | 'Scorekeeper';

  // Organization context
  organizationId?: string;
  organizationName?: string;
}

// My tournaments response with categorized tournaments
export interface MyTournamentsResponseDto {
  active: UserTournamentSummaryDto[];
  past: UserTournamentSummaryDto[];
  organizing: UserTournamentSummaryDto[];
}

// Filter options for user tournaments
export interface UserTournamentsFilter {
  filter?: 'all' | 'won';
  year?: number;
}

// ============================================
// Admin Types
// ============================================

// Response from admin password reset
export interface AdminPasswordResetResponse {
  userId: string;
  email: string;
  temporaryPassword: string;
  message: string;
}

// User search result for admin
export interface AdminUserSearchResult {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
}

// Change password request
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

// Forgot password request
export interface ForgotPasswordRequest {
  email: string;
}

// Forgot password response
export interface ForgotPasswordResponse {
  message: string;
}

// ============================================
// Tournament Audit Log Types (TRN-029)
// ============================================

// Audit log entry from the API
export interface TournamentAuditLogDto {
  id: string;
  tournamentId: string;
  userId: string;
  userName: string;
  action: string;
  actionDescription: string;  // Human-readable description
  fromStatus?: string;
  toStatus?: string;
  entityType?: string;
  entityId?: string;
  oldValue?: string;  // JSON string
  newValue?: string;  // JSON string
  details?: string;   // JSON string with additional info
  timestamp: string;  // ISO date string
}

// Paginated response for audit log list
export interface AuditLogListResponse {
  auditLogs: TournamentAuditLogDto[];
  totalCount: number;
  hasMore: boolean;
}

// Filter options for audit log queries
export interface AuditLogFilter {
  action?: string;
  fromDate?: string;  // ISO date string
  toDate?: string;    // ISO date string
}

// ============================================
// Tournament Tie Resolution Types (TRN-031)
// ============================================

// Single team placement for tie resolution
export interface TieResolutionItem {
  teamId: string;
  finalPlacement: number;
}

// Request to resolve ties manually
export interface ResolveTiesRequest {
  resolutions: TieResolutionItem[];
}
