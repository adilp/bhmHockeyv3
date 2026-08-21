// Skill levels
export const SKILL_LEVELS = ['Gold', 'Silver', 'Bronze', 'D-League'] as const;

// D-League teams. Optional: a D-League player may or may not be on a team.
// The color is the team's jersey color, shown as a dot beside the name.
export const DLEAGUE_TEAMS = [
  { name: 'Bombers', color: 'Red' },
  { name: 'Knuckleheads', color: 'Blue' },
  { name: 'Killer Bees', color: 'Gold' },
  { name: 'Molar Bears', color: 'Orange' },
  { name: 'Lawdog', color: 'White' },
  { name: 'Bandits', color: 'Black' },
] as const;

export const DLEAGUE_TEAM_NAMES = DLEAGUE_TEAMS.map((t) => t.name);

// Positions (simplified to Goalie and Skater)
export const POSITIONS = ['Goalie', 'Skater'] as const;

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
