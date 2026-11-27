namespace BHMHockey.Api.Models.Entities;

/// <summary>
/// One person's line in one game: who they were, and what they did.
///
/// Key: (GameResultId, SourcePlayerKey). SourcePlayerKey is AMP Vision's
/// permanent player id ("amp:18") or a loaner sweater ("amp-loaner:home:45"),
/// so a re-publish of the same game updates the same person's line.
///
/// UserId is set ONLY for a real, active app account (never a ghost: a ghost id
/// is new every game). A line with UserId = null is a person who is not in the
/// app: named in that game's recap as plain text, never linked, never on a
/// leaderboard. When they join, AMP Vision links them and re-publishes.
///
/// Counting stats are nullable on purpose: null = NOT KNOWN (assists nobody
/// ruled), 0 = known to be zero. The app prints "-" for null, never "0".
/// </summary>
public class GamePlayerLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameResultId { get; set; }
    public GameResult GameResult { get; set; } = null!;

    public string SourcePlayerKey { get; set; } = string.Empty;
    public int? AmpPlayerId { get; set; }          // null for a loaner sweater

    public Guid? UserId { get; set; }              // a real, non-ghost account, or null
    public User? User { get; set; }

    public string DisplayName { get; set; } = string.Empty; // snapshot, as AMP Vision named them
    public string Team { get; set; } = string.Empty;        // "Black" or "White"
    public string Position { get; set; } = "Skater";        // "Skater" or "Goalie" (RegisteredPosition)
    public string? Jersey { get; set; }                     // as worn, "07" != "7"
    public bool IsLoaner { get; set; }

    public int? Goals { get; set; }
    public int? Assists { get; set; }
    public int? Points { get; set; }
    public int? Star { get; set; }               // 1, 2, 3 or null
    public int? GoalsAgainst { get; set; }       // goalies; null when two dressed for a side
    public int? Saves { get; set; }              // null until a save count's recall is measured

    // Estimates: ONLY the worded text AMP Vision produced ("about 23 min",
    // "at least 1.9 mi") and its qualifier. No number is stored, so nothing can
    // sort, total or rank them. {"iceTime":{"display":"...","qualifier":"about"}}
    public string? EstimatesJson { get; set; }
}
