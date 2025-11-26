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
  organizationId: string;
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
  createdAt: string;
  updatedAt: string;
}

export type EventStatus = 'Draft' | 'Published' | 'Full' | 'Completed' | 'Cancelled';

export interface EventRegistration {
  id: string;
  eventId: string;
  userId: string;
  status: RegistrationStatus;
  registeredAt: string;
}

export type RegistrationStatus = 'Registered' | 'Cancelled';

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
