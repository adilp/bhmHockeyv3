namespace BHMHockey.Api.Models.DTOs;

// Game results, published by AMP Vision (the owner's video project) and read by
// the app's Results tab, player screen and organization leaders. Every type
// here is NEW: no existing endpoint returns or accepts any of them.
// Mirrored by hand in packages/shared/src/types/gameResults.ts.

// ---------------------------------------------------------------------------
// WRITE: PUT /api/events/{eventId}/result   (organization admin only)
// ---------------------------------------------------------------------------

public record GameResultUpsertRequest(
    string SchemaVersion,                       // "amp.game_result.v1" - anything else is refused
    GameResultSourceDto Source,
    Guid EventId,                               // must equal the route id
    GameResultTeamsDto Teams,
    string FinalScoreSource,                    // "person" | "counted_goals"
    int? PeriodCount,
    int? PeriodLengthSec,
    GameResultVideoDto? Video,
    List<GameGoalUpsertDto> Goals,
    List<GameStarUpsertDto> Stars,
    List<GamePlayerLineUpsertDto> Players,
    Dictionary<string, object>? Measurement,
    List<string>? Notes,
    string? EstimateDisclaimer
);

public record GameResultSourceDto(string System, string GameKey, int Revision,
    DateTime GeneratedAtUtc, string? CodeVersion);

public record GameResultTeamsDto(GameResultSideDto Black, GameResultSideDto White);
public record GameResultSideDto(string? Label, int Score);

public record GameResultVideoDto(string? Url, List<VideoChapterDto>? Chapters);
public record VideoChapterDto(string Label, int? OffsetSec);

public record PlayerKeyRef(string PlayerKey);

public record GameGoalUpsertDto(
    int Sequence, string Team, int? Period, int? PeriodElapsedSec,
    Dictionary<string, int>? ScoreAfter,
    PlayerKeyRef? Scorer, string? ScorerSource,
    List<PlayerKeyRef>? Assists, string AssistsSource,
    int? VideoOffsetSec);

public record GameStarUpsertDto(int Rank, string PlayerKey);

public record GamePlayerLineUpsertDto(
    string PlayerKey, int? AmpPlayerId, Guid? UserId, string? Link,
    string DisplayName, string Team, string Position, string? Jersey, bool IsLoaner,
    int? Goals, int? Assists, int? Points, int? Star, int? GoalsAgainst, int? Saves,
    Dictionary<string, GameEstimateDto>? Estimates,
    Dictionary<string, string>? EstimatesWithheld);   // reasons; logged only, stored nowhere

/// <summary>An estimate is TEXT ONLY ("about 23 min"). There is no number to sort or total.</summary>
public record GameEstimateDto(string Display, string Qualifier);

/// <summary>What the upsert did. Partial by design, as AwardBadgeResponse is.</summary>
public record GameResultUpsertResponse(
    string Outcome,                        // "created" | "updated" | "unchanged"
    int Revision,
    List<Guid> UnknownUserIds,             // stored as name-only lines (UserId cleared)
    List<Guid> GhostUserIds,               // refused as a link; stored as name-only lines
    List<Guid> NotRegisteredUserIds);      // linked, but no Registered row on this event (a note)

public record WithdrawGameResultRequest(string Reason, int Revision);

public record WithdrawGameResultResponse(string Outcome, int Revision); // "withdrawn" | "unchanged"

// ---------------------------------------------------------------------------
// READ: GET /api/events/{eventId}/result  (the Results tab)
// ---------------------------------------------------------------------------

public record GameResultDto(
    Guid EventId,
    string Status,                          // only "Published" is ever returned
    GameResultSideDto Black,
    GameResultSideDto White,
    string? Winner,                         // "Black" | "White" | null (tie)
    int? PeriodCount,
    string? VideoUrl,
    List<VideoChapterDto> VideoChapters,
    List<GameGoalDto> Goals,
    List<GamePlayerLineDto> Stars,          // rank order
    List<GamePlayerLineDto> BlackLines,
    List<GamePlayerLineDto> WhiteLines,
    List<string> Notes,
    string? EstimateDisclaimer,
    DateTime UpdatedAt);

public record GameGoalDto(
    int Sequence, string Team, int? Period, int? PeriodElapsedSec,
    int BlackScoreAfter, int WhiteScoreAfter,
    GamePlayerRefDto? Scorer, List<GamePlayerRefDto> Assists, bool AssistsRuled,
    int? VideoOffsetSec);

/// <summary>A name as it appears anywhere. UserId null = not in the app: plain text, no tap.</summary>
public record GamePlayerRefDto(Guid? UserId, string DisplayName, string? Jersey, bool IsLoaner);

public record GamePlayerLineDto(
    GamePlayerRefDto Player, string Team, string Position,
    int? Goals, int? Assists, int? Points, int? Star, int? GoalsAgainst, int? Saves,
    Dictionary<string, GameEstimateDto>? Estimates);

// ---------------------------------------------------------------------------
// READ: GET /api/users/{userId}/stats?season=2026   (player screen + sheet)
// ---------------------------------------------------------------------------

public record PlayerStatsDto(
    Guid UserId, string FirstName, string LastName,
    bool StatsHidden,                       // the player hid their stats: everything below is empty
    string Season,
    PlayerSeasonLineDto? SeasonLine,        // computed on read, Published results only
    PlayerSeasonLineDto? Career,
    List<PlayerGameLogEntryDto> GameLog)
{
    /// <summary>
    /// The season's other past games the player was registered for, with no published
    /// line for them (every game before 11/22/2026, and any later one without a result):
    /// the player screen lists them as "No stats". Newest first; only games the caller
    /// may see; empty when the stats are hidden.
    /// </summary>
    public List<PlayerGameWithoutStatsDto> GamesWithoutStats { get; init; } = new();
}

public record PlayerGameWithoutStatsDto(
    Guid EventId, DateTime EventDate, string? EventName,
    string? Team);                          // "Black" | "White", only once the roster was published

public record PlayerSeasonLineDto(
    int GamesWithStats,                     // games with a published result and a line for them
    int Goals, int Assists, int Points, int Stars,
    int? GoalsAgainst,                      // goalies only
    int GamesWithUnknownScorers);           // > 0: the totals are a FLOOR, and the screen says so

public record PlayerGameLogEntryDto(
    Guid EventId, DateTime EventDate, string? OpponentLabel, string Team,
    string Result,                          // "W" | "L" | "T"
    int TeamScore, int OpponentScore,
    int? Goals, int? Assists, int? Points, int? Star,
    Dictionary<string, GameEstimateDto>? Estimates);

/// <summary>GET/PUT /api/users/me/stats-visibility</summary>
public record StatsVisibilityDto(bool Hidden);

// ---------------------------------------------------------------------------
// READ: GET /api/organizations/{orgId}/leaderboard?season=2026&stat=points
// ---------------------------------------------------------------------------

public record LeaderboardDto(
    Guid OrganizationId, string Season, string Stat,   // "points" | "goals" | "assists" | "stars"
    int GamesWithStats,                                 // org games with a published result this season
    List<LeaderboardRowDto> Rows);

public record LeaderboardRowDto(
    int Rank, Guid UserId, string FirstName, string LastName,
    int GamesWithStats, int Goals, int Assists, int Points, int Stars);

// ---------------------------------------------------------------------------
// READ: GET /api/organizations/{orgId}/results?limit=5   (org page strip)
// ---------------------------------------------------------------------------

public record GameResultSummaryDto(
    Guid EventId, DateTime EventDate, string? EventName,
    GameResultSideDto Black, GameResultSideDto White);

// ---------------------------------------------------------------------------
// WRITE: POST /api/organizations/{orgId}/past-games   (organization admin only)
// A game that was played but never existed in the app (2026-11-22 was invite
// only). Creates the Event and its Registered roster with NO side effects: no
// notification, no auto-roster, no waitlist, no payment, no reminder.
// ---------------------------------------------------------------------------

public record CreatePastGameRequest(
    string? Name,
    string? Description,
    DateTime EventDate,                     // UTC; must be in the past
    int? Duration,                          // minutes, default 60
    string? Venue,
    string? Visibility,                     // default "OrganizationMembers"
    string SourceGameKey,                   // "2026-11-22_main": refuses a second import
    List<PastGameRosterEntry> Roster);

public record PastGameRosterEntry(Guid UserId, string Team, string Position); // Black|White, Skater|Goalie

public record CreatePastGameResponse(
    Guid EventId,
    string Outcome,                         // "created" | "already_exists"
    int RegisteredCount,
    List<Guid> SkippedUserIds);             // unknown, inactive or ghost accounts: not added
