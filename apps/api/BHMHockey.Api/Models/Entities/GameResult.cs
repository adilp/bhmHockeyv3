namespace BHMHockey.Api.Models.Entities;

/// <summary>
/// The result of one played game (an Event), as published by AMP Vision.
/// One row per Event (unique EventId). Children: PlayerLines, Goals.
///
/// Written ONLY by GameResultService.PutResultAsync (PUT /api/events/{id}/result),
/// replaced whole on each newer SourceRevision, and never hard-deleted by the
/// API: a withdrawal sets Status = "Withdrawn" and every read then behaves as if
/// there were no result. Nothing on Event changes, so no existing query, DTO or
/// screen sees this table.
///
/// The JSON columns are stored as text holding JSON (jsonb in PostgreSQL); only
/// GameResultService reads or writes them.
/// </summary>
public class GameResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    // Copied from Event.OrganizationId at write time so the org leaderboard is one
    // indexed query. A result is refused for an event with no organization.
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string Status { get; set; } = GameResultStatus.Published;

    // Sides use the app's own vocabulary (EventRegistration.TeamAssignment).
    // Label is what the side is called in this game ("Home", "Visitors"); null = "Black"/"White".
    public int BlackScore { get; set; }
    public int WhiteScore { get; set; }
    public string? BlackLabel { get; set; }
    public string? WhiteLabel { get; set; }

    // "person" = a person entered the final score; "counted_goals" = the sum of the goals
    public string FinalScoreSource { get; set; } = "person";

    public int? PeriodCount { get; set; }
    public int? PeriodLengthSec { get; set; }

    public string? VideoUrl { get; set; }
    public string? VideoChaptersJson { get; set; }   // [{"label":"Period 1","offsetSec":312}]
    public string? MeasurementJson { get; set; }     // {"savesMeasured":false,...}
    public string? NotesJson { get; set; }           // ["Saves not measured", ...]
    public string? EstimateDisclaimer { get; set; }

    // Provenance of the publication
    public string Source { get; set; } = "amp_vision";
    public string SourceGameKey { get; set; } = string.Empty;   // "2026-11-22_main"
    public int SourceRevision { get; set; }                     // strictly increasing per game
    public string? SourceCodeVersion { get; set; }              // AMP Vision commit
    public DateTime SourceGeneratedAt { get; set; }
    public string PayloadHash { get; set; } = string.Empty;     // SHA-256 of the normalized body
    public Guid PublishedByUserId { get; set; }

    public DateTime PublishedAt { get; set; } = DateTime.UtcNow; // first publication
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;   // last accepted revision
    public DateTime? WithdrawnAt { get; set; }
    public string? WithdrawnReason { get; set; }

    public ICollection<GamePlayerLine> PlayerLines { get; set; } = new List<GamePlayerLine>();
    public ICollection<GameGoal> Goals { get; set; } = new List<GameGoal>();
}

public static class GameResultStatus
{
    public const string Published = "Published";
    public const string Withdrawn = "Withdrawn";
}
