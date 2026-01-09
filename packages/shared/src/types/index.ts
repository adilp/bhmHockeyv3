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

// Member/subscriber info for admin view
export interface OrganizationMember {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  positions?: UserPositions;  // Multi-position support
  subscribedAt: string;
  isAdmin: boolean;  // True if this member is an admin of the organization
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
}

export interface UpdateOrganizationRequest {
  name?: string;
  description?: string;
  location?: string;
  skillLevels?: SkillLevel[];  // Multiple skill levels allowed
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
  badges: UserBadgeDto[];  // Top 3 badges by displayOrder
}
