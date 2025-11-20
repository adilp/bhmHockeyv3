// User types
export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  role: UserRole;
  pushToken?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export type UserRole = 'Player' | 'Organizer' | 'Admin';

// Organization types
export interface Organization {
  id: string;
  name: string;
  description?: string;
  creatorId: string;
  location?: string;
  skillLevel?: SkillLevel;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export type SkillLevel = 'Beginner' | 'Intermediate' | 'Advanced' | 'All';

export interface OrganizationSubscription {
  id: string;
  organizationId: string;
  userId: string;
  notificationEnabled: boolean;
  subscribedAt: string;
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
