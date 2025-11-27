// User types
export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  skillLevel?: SkillLevel;
  position?: Position;
  venmoHandle?: string;
  role: UserRole;
  pushToken?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export type UserRole = 'Player' | 'Organizer' | 'Admin';
export type Position = 'Forward' | 'Defense' | 'Goalie';

// Organization types
export interface Organization {
  id: string;
  name: string;
  description?: string;
  creatorId: string;
  location?: string;
  skillLevel?: SkillLevel;
  subscriberCount: number;
  isSubscribed: boolean;
  createdAt: string;
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
  skillLevel?: SkillLevel;
}

export interface UpdateOrganizationRequest {
  name?: string;
  description?: string;
  location?: string;
  skillLevel?: SkillLevel;
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

export type RegistrationStatus = 'Registered' | 'Cancelled';

// Payment status for event registrations (Phase 4)
export type PaymentStatus = 'Pending' | 'MarkedPaid' | 'Verified';

// EventDto - API response with computed fields
export interface EventDto {
  id: string;
  organizationId?: string;       // Optional - null for standalone events
  organizationName?: string;     // Optional - null for standalone events
  creatorId: string;             // Who created the event
  name: string;
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
  isRegistered: boolean;
  isCreator: boolean;            // True if current user created this event
  createdAt: string;
  // Payment fields (Phase 4)
  creatorVenmoHandle?: string;   // For "Pay with Venmo" button
  myPaymentStatus?: PaymentStatus; // Current user's payment status
}

// EventRegistrationDto - API response for registration with user details
export interface EventRegistrationDto {
  id: string;
  eventId: string;
  user: User;
  status: RegistrationStatus;
  registeredAt: string;
  // Payment fields (Phase 4)
  paymentStatus?: PaymentStatus;
  paymentMarkedAt?: string;
  paymentVerifiedAt?: string;
}

// Event request types
export interface CreateEventRequest {
  organizationId?: string;       // Optional - omit for standalone pickup games
  name: string;
  description?: string;
  eventDate: string;
  duration: number;
  venue?: string;
  maxPlayers: number;
  cost: number;
  registrationDeadline?: string;
  visibility?: EventVisibility;  // Default: 'Public'
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
}

// Payment request types (Phase 4)
export interface MarkPaymentRequest {
  paymentReference?: string;
}

export interface UpdatePaymentStatusRequest {
  paymentStatus: 'Verified' | 'Pending';
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
  skillLevel?: SkillLevel;
  position?: Position;
  venmoHandle?: string;
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
