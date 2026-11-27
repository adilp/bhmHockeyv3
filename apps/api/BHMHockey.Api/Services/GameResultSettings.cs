namespace BHMHockey.Api.Services;

/// <summary>
/// Runtime switches for game results, bound from the optional "GameResults"
/// configuration section (env vars GameResults__ReadsEnabled,
/// GameResults__WritesEnabled). Both default to true, so NO new configuration is
/// required at startup.
///
/// ReadsEnabled = false is the remote off switch for the whole feature: every
/// read endpoint answers 404, which the app treats exactly like "no stats yet"
/// and hides every new section. Changing it on DigitalOcean redeploys the API;
/// no app release is involved.
/// </summary>
public class GameResultOptions
{
    public bool ReadsEnabled { get; set; } = true;
    public bool WritesEnabled { get; set; } = true;
}

/// <summary>
/// The owner's decisions about game results, each one constant in one place.
/// Confirmed by the owner on 2026-11-27: results are written by an organization
/// admin (the owner's own account); members see the stats; a season is one
/// calendar year; stars award no badges; members may see another member's past
/// games that have no stats. Still open: exactly what the hide switch covers
/// (HiddenPlayersKeepNamesInRecap keeps its proposed default).
/// </summary>
public static class GameResultSettings
{
    /// <summary>The only body shape accepted by PUT /events/{id}/result.</summary>
    public const string SchemaVersion = "amp.game_result.v1";

    /// <summary>
    /// The owner, 2026-11-27: "We will NOT go back and look at previous games.
    /// Those are done and posted." Results exist for 2026-11-22 and later; an
    /// event before this date (Central time) is refused.
    /// </summary>
    public static readonly DateOnly EarliestGameDate = new(2026, 11, 22);

    /// <summary>What a season is: the calendar year of the event, in Central time.</summary>
    public static readonly TimeZoneInfo SeasonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    /// <summary>
    /// Who can see stats: members of the organization (subscribers and admins),
    /// as the Members list. The Results tab follows the event's own visibility.
    /// </summary>
    public const string StatsAudience = "OrganizationMembers";

    /// <summary>
    /// A player who hides their stats keeps their NAME on their goals and stars
    /// in a game's recap (a goal is a fact of a game other people played in);
    /// their box-score line, profile stats and leaderboard row are hidden.
    /// </summary>
    public const bool HiddenPlayersKeepNamesInRecap = true;

    /// <summary>Stars are stats only; no badge is awarded for them.</summary>
    public const bool StarsAwardBadges = false;

    public static readonly string[] LeaderboardStats = { "points", "goals", "assists", "stars" };
    public static readonly string[] EstimateQualifiers = { "about", "atLeast" };
    public const int RecentResultsMax = 20;
    /// <summary>
    /// Members of an organization may see another member's past games there that have
    /// no stats (the owner, 2026-11-27). At most this many "No stats" games on a player screen (one season of weekly games fits).</summary>
    public const int GamesWithoutStatsMax = 60;
    public const int MaxPlayersPerGame = 80;
    public const int MaxGoalsPerGame = 60;
}
