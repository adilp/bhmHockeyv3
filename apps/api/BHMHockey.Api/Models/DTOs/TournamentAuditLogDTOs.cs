namespace BHMHockey.Api.Models.DTOs;

/// <summary>
/// Response DTO for tournament audit log entries
/// </summary>
public record TournamentAuditLogDto
{
    public required Guid Id { get; init; }
    public required Guid TournamentId { get; init; }
    public required Guid UserId { get; init; }
    public required string UserName { get; init; }
    public required string Action { get; init; }
    public required string ActionDescription { get; init; }
    public string? FromStatus { get; init; }
    public string? ToStatus { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? Details { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Paginated response for audit log list
/// </summary>
public record AuditLogListResponse
{
    public required List<TournamentAuditLogDto> AuditLogs { get; init; }
    public required int TotalCount { get; init; }
    public required bool HasMore { get; init; }
}

/// <summary>
/// Request DTO for filtering audit logs (used as query parameters)
/// </summary>
public record AuditLogFilterRequest
{
    /// <summary>
    /// Filter by action type (e.g., "Publish", "Start", "Cancel")
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Filter by date range start (inclusive)
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Filter by date range end (inclusive)
    /// </summary>
    public DateTime? ToDate { get; set; }
}
