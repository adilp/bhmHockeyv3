using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface ITournamentAnnouncementService
{
    Task<List<TournamentAnnouncementDto>> GetAnnouncementsAsync(Guid tournamentId, Guid? requesterId);
    Task<TournamentAnnouncementDto> CreateAnnouncementAsync(Guid tournamentId, CreateTournamentAnnouncementRequest request, Guid requesterId);
    Task<TournamentAnnouncementDto?> UpdateAnnouncementAsync(Guid tournamentId, Guid announcementId, UpdateTournamentAnnouncementRequest request, Guid requesterId);
    Task<bool> DeleteAnnouncementAsync(Guid tournamentId, Guid announcementId, Guid requesterId);
}
