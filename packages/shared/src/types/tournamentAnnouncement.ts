// ============================================
// Tournament Announcement Types
// ============================================

// Target options for announcements
export type AnnouncementTarget = 'All' | 'Captains' | 'Admins';

// Response DTO
export interface TournamentAnnouncementDto {
  id: string;
  tournamentId: string;
  title: string;
  body: string;
  target: AnnouncementTarget | null;  // null when targeting specific teams
  targetTeamIds: string[] | null;     // Team IDs when targeting specific teams
  createdByUserId: string;
  createdByFirstName: string;
  createdByLastName: string;
  createdAt: string;  // ISO date string
  updatedAt: string | null;
}

// Create request
export interface CreateTournamentAnnouncementRequest {
  title: string;
  body: string;
  target?: AnnouncementTarget | null;
  targetTeamIds?: string[] | null;
}

// Update request
export interface UpdateTournamentAnnouncementRequest {
  title?: string;
  body?: string;
  target?: AnnouncementTarget | null;
  targetTeamIds?: string[] | null;
}
