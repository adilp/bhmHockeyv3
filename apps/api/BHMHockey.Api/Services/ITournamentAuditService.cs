using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for managing tournament audit logs.
/// Centralizes all audit log creation and retrieval operations.
/// </summary>
public interface ITournamentAuditService
{
    /// <summary>
    /// Logs an audit entry for a tournament action.
    /// Does NOT call SaveChangesAsync - the calling service should handle transaction management.
    /// </summary>
    /// <param name="tournamentId">The tournament ID</param>
    /// <param name="userId">The user performing the action</param>
    /// <param name="action">The action being performed (e.g., "Publish", "Start", "DeleteTeam")</param>
    /// <param name="entityType">Optional entity type being affected (e.g., "Team", "TeamMember")</param>
    /// <param name="entityId">Optional entity ID being affected</param>
    /// <param name="oldValue">Optional JSON of the previous state</param>
    /// <param name="newValue">Optional JSON of the new state</param>
    /// <param name="fromStatus">Optional status before transition</param>
    /// <param name="toStatus">Optional status after transition</param>
    /// <param name="details">Optional JSON details about the action</param>
    Task LogAsync(
        Guid tournamentId,
        Guid userId,
        string action,
        string? entityType = null,
        Guid? entityId = null,
        string? oldValue = null,
        string? newValue = null,
        string? fromStatus = null,
        string? toStatus = null,
        string? details = null);

    /// <summary>
    /// Gets paginated audit logs for a tournament with optional filters.
    /// Requires tournament admin access.
    /// </summary>
    /// <param name="tournamentId">The tournament ID</param>
    /// <param name="userId">The user requesting the logs (for authorization check)</param>
    /// <param name="offset">Number of records to skip (default: 0)</param>
    /// <param name="limit">Maximum number of records to return (default: 20, max: 50)</param>
    /// <param name="actionFilter">Optional filter by action type</param>
    /// <param name="fromDate">Optional filter by date range start (inclusive)</param>
    /// <param name="toDate">Optional filter by date range end (inclusive)</param>
    /// <returns>Paginated list of audit log entries</returns>
    Task<AuditLogListResponse> GetAuditLogsAsync(
        Guid tournamentId,
        Guid userId,
        int offset = 0,
        int limit = 20,
        string? actionFilter = null,
        DateTime? fromDate = null,
        DateTime? toDate = null);
}
