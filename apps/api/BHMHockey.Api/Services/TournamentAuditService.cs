using System.Text.Json;
using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class TournamentAuditService : ITournamentAuditService
{
    private readonly AppDbContext _context;
    private readonly ITournamentAuthorizationService _authService;
    private readonly ILogger<TournamentAuditService> _logger;

    public TournamentAuditService(
        AppDbContext context,
        ITournamentAuthorizationService authService,
        ILogger<TournamentAuditService> logger)
    {
        _context = context;
        _authService = authService;
        _logger = logger;
    }

    public Task LogAsync(
        Guid tournamentId,
        Guid userId,
        string action,
        string? entityType = null,
        Guid? entityId = null,
        string? oldValue = null,
        string? newValue = null,
        string? fromStatus = null,
        string? toStatus = null,
        string? details = null)
    {
        var auditLog = new TournamentAuditLog
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = oldValue,
            NewValue = newValue,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        _context.TournamentAuditLogs.Add(auditLog);

        _logger.LogInformation(
            "Created audit log for tournament {TournamentId}: {Action} by user {UserId}",
            tournamentId, action, userId);

        // Don't call SaveChangesAsync - let the calling service handle the transaction
        return Task.CompletedTask;
    }

    public async Task<AuditLogListResponse> GetAuditLogsAsync(
        Guid tournamentId,
        Guid userId,
        int offset = 0,
        int limit = 20,
        string? actionFilter = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        // Check if user has access to view audit logs (requires at least Scorekeeper role)
        var hasAccess = await _authService.IsScorekeeperAsync(tournamentId, userId);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You are not authorized to view audit logs for this tournament");
        }

        // Clamp limit to prevent abuse
        limit = Math.Min(limit, 50);

        // Build query with filters
        var query = _context.TournamentAuditLogs
            .Include(a => a.User)
            .Where(a => a.TournamentId == tournamentId);

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a => a.Action == actionFilter);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            // Include the entire day for toDate by adding 1 day and using less than
            var endOfDay = toDate.Value.Date.AddDays(1);
            query = query.Where(a => a.Timestamp < endOfDay);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Fetch paginated results ordered by timestamp descending (newest first)
        var auditLogs = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        // Map to DTOs
        var auditLogDtos = auditLogs.Select(a => new TournamentAuditLogDto
        {
            Id = a.Id,
            TournamentId = a.TournamentId,
            UserId = a.UserId,
            UserName = a.User != null ? $"{a.User.FirstName} {a.User.LastName}" : "Unknown User",
            Action = a.Action,
            ActionDescription = FormatActionDescription(a),
            FromStatus = a.FromStatus,
            ToStatus = a.ToStatus,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            OldValue = a.OldValue,
            NewValue = a.NewValue,
            Details = a.Details,
            Timestamp = a.Timestamp
        }).ToList();

        return new AuditLogListResponse
        {
            AuditLogs = auditLogDtos,
            TotalCount = totalCount,
            HasMore = offset + limit < totalCount
        };
    }

    /// <summary>
    /// Formats a human-readable description of the audit log action
    /// </summary>
    private static string FormatActionDescription(TournamentAuditLog log)
    {
        // For status transitions
        if (!string.IsNullOrEmpty(log.FromStatus) && !string.IsNullOrEmpty(log.ToStatus))
        {
            return $"{log.Action}: {log.FromStatus} → {log.ToStatus}";
        }

        // For entity-specific actions
        if (!string.IsNullOrEmpty(log.EntityType))
        {
            return log.Action switch
            {
                "DeleteTeam" => $"Deleted team",
                "SetCaptain" => $"Set team captain",
                "RemoveCaptain" => $"Removed team captain",
                "AddTeamMember" => $"Added team member",
                "RemoveTeamMember" => $"Removed team member",
                _ => $"{log.Action} ({log.EntityType})"
            };
        }

        // Default to action name with spacing
        return AddSpacesToPascalCase(log.Action);
    }

    /// <summary>
    /// Adds spaces to PascalCase strings (e.g., "CloseRegistration" -> "Close Registration")
    /// </summary>
    private static string AddSpacesToPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var result = new System.Text.StringBuilder();
        result.Append(text[0]);

        for (int i = 1; i < text.Length; i++)
        {
            if (char.IsUpper(text[i]))
            {
                result.Append(' ');
            }
            result.Append(text[i]);
        }

        return result.ToString();
    }
}
