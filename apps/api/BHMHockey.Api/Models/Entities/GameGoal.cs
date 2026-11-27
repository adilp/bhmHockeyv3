namespace BHMHockey.Api.Models.Entities;

/// <summary>
/// One goal of a game, in the order it was scored. Unique (GameResultId, Sequence).
/// Scorer and assists point at the game's own lines, so a person not in the app
/// can still be named in the recap without being a User.
/// </summary>
public class GameGoal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameResultId { get; set; }
    public GameResult GameResult { get; set; } = null!;

    public int Sequence { get; set; }                 // 1..n
    public string Team { get; set; } = string.Empty;  // "Black" or "White"
    public int? Period { get; set; }
    public int? PeriodElapsedSec { get; set; }        // time into the period (running clock)
    public int BlackScoreAfter { get; set; }
    public int WhiteScoreAfter { get; set; }

    public Guid? ScorerLineId { get; set; }           // null = no identified scorer
    public GamePlayerLine? ScorerLine { get; set; }
    public string? ScorerSource { get; set; }         // "person_click"

    public Guid? PrimaryAssistLineId { get; set; }
    public GamePlayerLine? PrimaryAssistLine { get; set; }
    public Guid? SecondaryAssistLineId { get; set; }
    public GamePlayerLine? SecondaryAssistLine { get; set; }
    // "person_ruled": no assist lines = UNASSISTED. "not_ruled": assists unknown.
    public string AssistsSource { get; set; } = "not_ruled";

    public int? VideoOffsetSec { get; set; }          // jump-to-goal in GameResult.VideoUrl
}
