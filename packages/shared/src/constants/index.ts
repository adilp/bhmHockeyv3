// Skill levels
export const SKILL_LEVELS = ['Beginner', 'Intermediate', 'Advanced', 'All'] as const;

// Event statuses
export const EVENT_STATUSES = ['Draft', 'Published', 'Full', 'Completed', 'Cancelled'] as const;

// Registration statuses
export const REGISTRATION_STATUSES = ['Registered', 'Cancelled'] as const;

// User roles
export const USER_ROLES = ['Player', 'Organizer', 'Admin'] as const;

// Validation constants
export const VALIDATION = {
  PASSWORD_MIN_LENGTH: 8,
  PASSWORD_REGEX: /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]/,
  EMAIL_REGEX: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
  PHONE_REGEX: /^\d{10}$/,
} as const;

// Default values
export const DEFAULTS = {
  EVENT_DURATION: 60, // minutes
  MAX_PLAYERS: 20,
  EVENT_COST: 0,
} as const;
