using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Models.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BHMHockey.Api.Services;

/// <summary>See IGameResultService for the rules.</summary>
public class GameResultService : IGameResultService
{
    private const string Black = "Black";
    private const string White = "White";
    private const string PersonRuled = "person_ruled";
    private const string NotRuled = "not_ruled";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _context;
    private readonly IOrganizationAdminService _adminService;
    private readonly IEventVisibility _visibility;
    private readonly GameResultOptions _options;
    private readonly ILogger<GameResultService> _logger;

    public GameResultService(
        AppDbContext context,
        IOrganizationAdminService adminService,
        IEventVisibility visibility,
        IOptions<GameResultOptions> options,
        ILogger<GameResultService> logger)
    {
        _context = context;
        _adminService = adminService;
        _visibility = visibility;
        _options = options.Value;
        _logger = logger;
    }

    // =====================================================================
    // WRITE
    // =====================================================================

    public async Task<GameResultUpsertResponse?> PutResultAsync(Guid eventId, GameResultUpsertRequest request, Guid callerId)
    {
        if (!_options.WritesEnabled)
        {
            throw new InvalidOperationException("Game result writes are switched off on this server");
        }

        var evt = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (evt == null)
        {
            _logger.LogWarning("Put result for unknown event {EventId}", eventId);
            return null;
        }
        await RequireAdminOfEventOrgAsync(evt, callerId);
        RequirePlayedGame(evt);

        var problem = Validate(request, eventId);
        if (problem != null)
        {
            _logger.LogWarning("Put result refused for event {EventId}: {Problem}", eventId, problem);
            throw new InvalidOperationException(problem);
        }

        var hash = PayloadHash(request);
        var revision = request.Source.Revision;

        var clash = await _context.GameResults.AnyAsync(r =>
            r.Source == request.Source.System && r.SourceGameKey == request.Source.GameKey && r.EventId != eventId);
        if (clash)
        {
            throw new InvalidOperationException(
                $"Game key '{request.Source.GameKey}' is already published on another event");
        }

        var existing = await _context.GameResults
            .Include(r => r.PlayerLines)
            .Include(r => r.Goals)
            .FirstOrDefaultAsync(r => r.EventId == eventId);

        if (existing != null)
        {
            if (revision < existing.SourceRevision)
            {
                throw new StaleRevisionException(
                    $"Revision {revision} is older than the stored revision {existing.SourceRevision}",
                    existing.SourceRevision);
            }
            if (revision == existing.SourceRevision)
            {
                if (existing.PayloadHash == hash && existing.Status == GameResultStatus.Published)
                {
                    var unchangedReport = await ResolveUsersAsync(evt, request.Players);
                    return new GameResultUpsertResponse("unchanged", revision,
                        unchangedReport.Unknown, unchangedReport.Ghosts, unchangedReport.NotRegistered);
                }
                throw new StaleRevisionException(
                    $"Revision {revision} is already stored with a different body (or withdrawn); send a higher revision",
                    existing.SourceRevision);
            }
        }

        var report = await ResolveUsersAsync(evt, request.Players);
        foreach (var (key, reason) in request.Players
                     .Where(p => p.EstimatesWithheld != null)
                     .SelectMany(p => p.EstimatesWithheld!.Select(w => (p.PlayerKey, $"{w.Key}: {w.Value}"))))
        {
            _logger.LogInformation("Result {GameKey} r{Revision}: estimate withheld for {PlayerKey} ({Reason})",
                request.Source.GameKey, revision, key, reason);
        }

        var now = DateTime.UtcNow;
        var isRelational = _context.Database.IsRelational();
        await using var transaction = isRelational ? await _context.Database.BeginTransactionAsync() : null;
        try
        {
            GameResult result;
            string outcome;
            if (existing == null)
            {
                result = new GameResult { EventId = eventId, PublishedAt = now };
                _context.GameResults.Add(result);
                outcome = "created";
            }
            else
            {
                result = existing;
                // Goals point at lines: remove goals first, then lines
                _context.GameGoals.RemoveRange(existing.Goals);
                await _context.SaveChangesAsync();
                _context.GamePlayerLines.RemoveRange(existing.PlayerLines);
                await _context.SaveChangesAsync();
                outcome = "updated";
            }

            ApplyHeader(result, evt, request, hash, callerId, now);
            var linesByKey = BuildLines(result, request, report.Valid);
            foreach (var line in linesByKey.Values) _context.GamePlayerLines.Add(line);
            await _context.SaveChangesAsync();

            foreach (var goal in BuildGoals(result, request, linesByKey)) _context.GameGoals.Add(goal);
            await _context.SaveChangesAsync();

            if (transaction != null) await transaction.CommitAsync();

            _logger.LogInformation(
                "Result {Outcome} for event {EventId} ({GameKey} r{Revision}) by {UserId}: {Lines} lines, {Goals} goals",
                outcome, eventId, request.Source.GameKey, revision, callerId, linesByKey.Count, request.Goals.Count);
            return new GameResultUpsertResponse(outcome, revision, report.Unknown, report.Ghosts, report.NotRegistered);
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<WithdrawGameResultResponse?> WithdrawResultAsync(Guid eventId, WithdrawGameResultRequest request, Guid callerId)
    {
        if (!_options.WritesEnabled)
        {
            throw new InvalidOperationException("Game result writes are switched off on this server");
        }
        var evt = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (evt == null) return null;
        await RequireAdminOfEventOrgAsync(evt, callerId);

        var existing = await _context.GameResults.FirstOrDefaultAsync(r => r.EventId == eventId);
        if (existing == null) return null;

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
        {
            throw new InvalidOperationException("A withdrawal needs a reason of at most 500 characters");
        }
        if (existing.Status == GameResultStatus.Withdrawn && request.Revision == existing.SourceRevision)
        {
            return new WithdrawGameResultResponse("unchanged", existing.SourceRevision);
        }
        if (request.Revision <= existing.SourceRevision)
        {
            throw new StaleRevisionException(
                $"A withdrawal must carry a revision above the stored {existing.SourceRevision}",
                existing.SourceRevision);
        }

        existing.Status = GameResultStatus.Withdrawn;
        existing.SourceRevision = request.Revision;
        existing.WithdrawnAt = DateTime.UtcNow;
        existing.WithdrawnReason = request.Reason.Trim();
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Result withdrawn for event {EventId} r{Revision} by {UserId}: {Reason}",
            eventId, request.Revision, callerId, existing.WithdrawnReason);
        return new WithdrawGameResultResponse("withdrawn", request.Revision);
    }

    private async Task RequireAdminOfEventOrgAsync(Event evt, Guid callerId)
    {
        if (!evt.OrganizationId.HasValue)
        {
            throw new InvalidOperationException("Results are kept only for organization events");
        }
        if (!await _adminService.IsUserAdminAsync(evt.OrganizationId.Value, callerId))
        {
            _logger.LogWarning("Result write denied for event {EventId}: {UserId} is not an org admin", evt.Id, callerId);
            throw new UnauthorizedAccessException("Only an admin of the event's organization can publish its result");
        }
    }

    private static void RequirePlayedGame(Event evt)
    {
        if (evt.Status == "Draft" || evt.Status == "Cancelled")
        {
            throw new InvalidOperationException($"A {evt.Status} event has no result");
        }
        if (evt.EventDate > DateTime.UtcNow)
        {
            throw new InvalidOperationException("The game has not been played yet");
        }
        var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(evt.EventDate, DateTimeKind.Utc), GameResultSettings.SeasonTimeZone));
        if (local < GameResultSettings.EarliestGameDate)
        {
            throw new InvalidOperationException(
                $"Results start with the {GameResultSettings.EarliestGameDate:yyyy-MM-dd} game; older games are not published");
        }
    }

    /// <summary>The first problem with the body, or null. Pure: no database.</summary>
    public static string? Validate(GameResultUpsertRequest r, Guid routeEventId)
    {
        if (r == null) return "Missing body";
        if (r.SchemaVersion != GameResultSettings.SchemaVersion)
            return $"Unsupported schemaVersion '{r.SchemaVersion}' (expected {GameResultSettings.SchemaVersion})";
        if (r.EventId != routeEventId) return "Body eventId does not match the route";
        if (r.Source == null || string.IsNullOrWhiteSpace(r.Source.System) || r.Source.System.Length > 50)
            return "source.system is required (at most 50 characters)";
        if (string.IsNullOrWhiteSpace(r.Source.GameKey) || r.Source.GameKey.Length > 100)
            return "source.gameKey is required (at most 100 characters)";
        if (r.Source.Revision < 1) return "source.revision must be 1 or more";
        if (r.Source.CodeVersion is { Length: > 64 }) return "source.codeVersion is at most 64 characters";
        if (r.Teams?.Black == null || r.Teams.White == null) return "teams.black and teams.white are required";
        if (r.Teams.Black.Score < 0 || r.Teams.White.Score < 0) return "Scores cannot be negative";
        if (r.Teams.Black.Label is { Length: > 50 } || r.Teams.White.Label is { Length: > 50 })
            return "A side's label is at most 50 characters";
        if (r.FinalScoreSource != "person" && r.FinalScoreSource != "counted_goals")
            return "finalScoreSource must be 'person' or 'counted_goals'";
        if (r.PeriodCount is < 1 or > 10) return "periodCount must be 1-10";
        if (r.PeriodLengthSec is < 1) return "periodLengthSec must be positive";
        if (r.Video?.Url is { Length: > 500 }) return "video.url is at most 500 characters";
        if (r.Video?.Url != null
            && (!Uri.TryCreate(r.Video.Url, UriKind.Absolute, out var videoUri) || videoUri.Scheme != Uri.UriSchemeHttps))
            return "video.url must be an absolute https URL";
        if (r.Video?.Chapters != null)
        {
            if (r.Video.Chapters.Count > 20) return "At most 20 video chapters";
            if (r.Video.Chapters.Any(c => c == null || string.IsNullOrWhiteSpace(c.Label) || c.Label.Length > 60 || c.OffsetSec is < 0))
                return "Each video chapter needs a label (at most 60 characters) and a non-negative offset";
        }
        if (r.Notes != null && (r.Notes.Count > 20 || r.Notes.Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 200)))
            return "At most 20 notes of at most 200 characters";
        if (r.EstimateDisclaimer is { Length: > 2000 }) return "estimateDisclaimer is at most 2000 characters";

        var players = r.Players ?? new List<GamePlayerLineUpsertDto>();
        var goals = r.Goals ?? new List<GameGoalUpsertDto>();
        var stars = r.Stars ?? new List<GameStarUpsertDto>();
        if (players.Count > GameResultSettings.MaxPlayersPerGame) return "Too many player lines";
        if (goals.Count > GameResultSettings.MaxGoalsPerGame) return "Too many goals";

        var lines = new Dictionary<string, GamePlayerLineUpsertDto>(StringComparer.Ordinal);
        foreach (var p in players)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.PlayerKey) || p.PlayerKey.Length > 64)
                return "Every player needs a playerKey of at most 64 characters";
            if (!lines.TryAdd(p.PlayerKey, p)) return $"Player key '{p.PlayerKey}' appears twice";
            if (NormalizeTeam(p.Team) == null) return $"Player '{p.PlayerKey}': team must be black or white";
            if (NormalizePosition(p.Position) == null) return $"Player '{p.PlayerKey}': position must be Skater or Goalie";
            if (string.IsNullOrWhiteSpace(p.DisplayName) || p.DisplayName.Length > 200)
                return $"Player '{p.PlayerKey}': displayName is required (at most 200 characters)";
            if (p.Jersey is { Length: > 4 }) return $"Player '{p.PlayerKey}': jersey is at most 4 characters";
            if (p.Goals < 0 || p.Assists < 0 || p.Points < 0 || p.GoalsAgainst < 0 || p.Saves < 0)
                return $"Player '{p.PlayerKey}': a count cannot be negative";
            if (p.Goals.HasValue && p.Assists.HasValue && p.Points.HasValue && p.Points != p.Goals + p.Assists)
                return $"Player '{p.PlayerKey}': points must equal goals + assists";
            if (p.Star is not null and not (1 or 2 or 3)) return $"Player '{p.PlayerKey}': star must be 1, 2 or 3";
            if (p.Estimates != null)
            {
                foreach (var (key, est) in p.Estimates)
                {
                    if (string.IsNullOrWhiteSpace(key) || key.Length > 32) return $"Player '{p.PlayerKey}': bad estimate key";
                    if (est == null || string.IsNullOrWhiteSpace(est.Display) || est.Display.Length > 40)
                        return $"Player '{p.PlayerKey}': estimate '{key}' needs display text of at most 40 characters";
                    if (!GameResultSettings.EstimateQualifiers.Contains(est.Qualifier))
                        return $"Player '{p.PlayerKey}': estimate '{key}' qualifier must be about or atLeast";
                }
            }
        }

        var sequences = goals.Select(g => g?.Sequence ?? 0).OrderBy(s => s).ToList();
        if (!sequences.SequenceEqual(Enumerable.Range(1, goals.Count)))
            return "Goal sequences must be 1..n, each once";

        int black = 0, white = 0;
        var goalsBy = new Dictionary<string, int>();
        var assistsBy = new Dictionary<string, int>();
        foreach (var g in goals.OrderBy(g => g.Sequence))
        {
            var team = NormalizeTeam(g.Team);
            if (team == null) return $"Goal {g.Sequence}: team must be black or white";
            if (team == Black) black++; else white++;
            if (g.ScoreAfter != null)
            {
                var sb = g.ScoreAfter.FirstOrDefault(kv => kv.Key.Equals("black", StringComparison.OrdinalIgnoreCase)).Value;
                var sw = g.ScoreAfter.FirstOrDefault(kv => kv.Key.Equals("white", StringComparison.OrdinalIgnoreCase)).Value;
                if (sb != black || sw != white) return $"Goal {g.Sequence}: scoreAfter does not follow from the goals before it";
            }
            if (g.Period is < 1 or > 10) return $"Goal {g.Sequence}: period must be 1-10";
            if (g.PeriodElapsedSec is < 0) return $"Goal {g.Sequence}: periodElapsedSec cannot be negative";
            if (g.VideoOffsetSec is < 0) return $"Goal {g.Sequence}: videoOffsetSec cannot be negative";
            if (g.AssistsSource != PersonRuled && g.AssistsSource != NotRuled)
                return $"Goal {g.Sequence}: assistsSource must be person_ruled or not_ruled";
            var assists = g.Assists ?? new List<PlayerKeyRef>();
            if (g.AssistsSource == NotRuled && assists.Count > 0)
                return $"Goal {g.Sequence}: assists that nobody ruled cannot be sent";
            if (assists.Count > 2) return $"Goal {g.Sequence}: at most two assists";
            if (g.Scorer != null)
            {
                if (!lines.TryGetValue(g.Scorer.PlayerKey ?? "", out var s)) return $"Goal {g.Sequence}: scorer is not a player line";
                if (NormalizeTeam(s.Team) != team) return $"Goal {g.Sequence}: scorer is on the other side";
                goalsBy[s.PlayerKey] = goalsBy.GetValueOrDefault(s.PlayerKey) + 1;
            }
            var seen = new HashSet<string>();
            foreach (var a in assists)
            {
                if (a == null || !lines.TryGetValue(a.PlayerKey ?? "", out var al)) return $"Goal {g.Sequence}: an assist is not a player line";
                if (NormalizeTeam(al.Team) != team) return $"Goal {g.Sequence}: an assist is on the other side";
                if (g.Scorer != null && a.PlayerKey == g.Scorer.PlayerKey) return $"Goal {g.Sequence}: the scorer cannot assist himself";
                if (!seen.Add(a.PlayerKey!)) return $"Goal {g.Sequence}: one player assists twice";
                assistsBy[a.PlayerKey!] = assistsBy.GetValueOrDefault(a.PlayerKey!) + 1;
            }
        }
        if (black != r.Teams.Black.Score || white != r.Teams.White.Score)
            return $"The score ({r.Teams.Black.Score}-{r.Teams.White.Score}) does not equal the goals per side ({black}-{white})";

        foreach (var p in players)
        {
            if (p.Goals.HasValue && p.Goals != goalsBy.GetValueOrDefault(p.PlayerKey))
                return $"Player '{p.PlayerKey}': goals do not match the goals list";
            if (p.Assists.HasValue && p.Assists != assistsBy.GetValueOrDefault(p.PlayerKey))
                return $"Player '{p.PlayerKey}': assists do not match the goals list";
        }

        if (stars.Count > 3) return "At most three stars";
        var ranks = new HashSet<int>();
        var starKeys = new HashSet<string>();
        foreach (var st in stars)
        {
            if (st == null || st.Rank is < 1 or > 3 || !ranks.Add(st.Rank)) return "Star ranks must be 1, 2, 3, each once";
            if (!lines.TryGetValue(st.PlayerKey ?? "", out var sl)) return $"Star {st.Rank} is not a player line";
            if (!starKeys.Add(st.PlayerKey!)) return "One player holds two stars";
            if (sl.Star.HasValue && sl.Star != st.Rank) return $"Star {st.Rank} disagrees with the line's own star";
        }
        if (players.Any(p => p.Star.HasValue && !stars.Any(s => s.PlayerKey == p.PlayerKey)))
            return "A line carries a star that the stars list does not";

        return null;
    }

    private record UserReport(HashSet<Guid> Valid, List<Guid> Unknown, List<Guid> Ghosts, List<Guid> NotRegistered);

    private async Task<UserReport> ResolveUsersAsync(Event evt, List<GamePlayerLineUpsertDto> players)
    {
        var ids = players.Where(p => p.UserId.HasValue).Select(p => p.UserId!.Value).Distinct().ToList();
        var users = await _context.Users.Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.IsActive, u.IsGhostPlayer }).ToListAsync();
        var registered = await _context.EventRegistrations
            .Where(r => r.EventId == evt.Id && r.Status == "Registered" && ids.Contains(r.UserId))
            .Select(r => r.UserId).ToListAsync();

        var valid = new HashSet<Guid>();
        var unknown = new List<Guid>();
        var ghosts = new List<Guid>();
        foreach (var id in ids)
        {
            var u = users.FirstOrDefault(x => x.Id == id);
            if (u == null || !u.IsActive) unknown.Add(id);
            else if (u.IsGhostPlayer) ghosts.Add(id);
            else valid.Add(id);
        }
        var notRegistered = valid.Where(id => !registered.Contains(id)).ToList();
        if (unknown.Count + ghosts.Count > 0)
        {
            _logger.LogWarning("Result for event {EventId}: {Unknown} unknown and {Ghosts} ghost user ids stored as name-only lines",
                evt.Id, unknown.Count, ghosts.Count);
        }
        return new UserReport(valid, unknown, ghosts, notRegistered);
    }

    private static void ApplyHeader(GameResult result, Event evt, GameResultUpsertRequest r, string hash, Guid callerId, DateTime now)
    {
        result.OrganizationId = evt.OrganizationId!.Value;
        result.Status = GameResultStatus.Published;
        result.BlackScore = r.Teams.Black.Score;
        result.WhiteScore = r.Teams.White.Score;
        result.BlackLabel = string.IsNullOrWhiteSpace(r.Teams.Black.Label) ? null : r.Teams.Black.Label.Trim();
        result.WhiteLabel = string.IsNullOrWhiteSpace(r.Teams.White.Label) ? null : r.Teams.White.Label.Trim();
        result.FinalScoreSource = r.FinalScoreSource;
        result.PeriodCount = r.PeriodCount;
        result.PeriodLengthSec = r.PeriodLengthSec;
        result.VideoUrl = r.Video?.Url;
        result.VideoChaptersJson = r.Video?.Chapters is { Count: > 0 } ch ? JsonSerializer.Serialize(ch, Json) : null;
        result.MeasurementJson = r.Measurement is { Count: > 0 } m ? JsonSerializer.Serialize(m, Json) : null;
        result.NotesJson = r.Notes is { Count: > 0 } n ? JsonSerializer.Serialize(n, Json) : null;
        result.EstimateDisclaimer = r.EstimateDisclaimer;
        result.Source = r.Source.System;
        result.SourceGameKey = r.Source.GameKey;
        result.SourceRevision = r.Source.Revision;
        result.SourceCodeVersion = r.Source.CodeVersion;
        result.SourceGeneratedAt = DateTime.SpecifyKind(r.Source.GeneratedAtUtc.ToUniversalTime(), DateTimeKind.Utc);
        result.PayloadHash = hash;
        result.PublishedByUserId = callerId;
        result.UpdatedAt = now;
        result.WithdrawnAt = null;
        result.WithdrawnReason = null;
    }

    private static Dictionary<string, GamePlayerLine> BuildLines(GameResult result, GameResultUpsertRequest r, HashSet<Guid> validUsers)
    {
        var starOf = (r.Stars ?? new()).ToDictionary(s => s.PlayerKey, s => s.Rank);
        var lines = new Dictionary<string, GamePlayerLine>(StringComparer.Ordinal);
        foreach (var p in r.Players ?? new())
        {
            lines[p.PlayerKey] = new GamePlayerLine
            {
                GameResultId = result.Id,
                SourcePlayerKey = p.PlayerKey,
                AmpPlayerId = p.AmpPlayerId,
                UserId = p.UserId.HasValue && validUsers.Contains(p.UserId.Value) ? p.UserId : null,
                DisplayName = p.DisplayName.Trim(),
                Team = NormalizeTeam(p.Team)!,
                Position = NormalizePosition(p.Position)!,
                Jersey = p.Jersey,
                IsLoaner = p.IsLoaner,
                Goals = p.Goals,
                Assists = p.Assists,
                Points = p.Points,
                Star = starOf.TryGetValue(p.PlayerKey, out var rank) ? rank : null,
                GoalsAgainst = p.GoalsAgainst,
                Saves = p.Saves,
                EstimatesJson = p.Estimates is { Count: > 0 } e ? JsonSerializer.Serialize(e, Json) : null,
            };
        }
        return lines;
    }

    private static IEnumerable<GameGoal> BuildGoals(GameResult result, GameResultUpsertRequest r, Dictionary<string, GamePlayerLine> lines)
    {
        int black = 0, white = 0;
        foreach (var g in (r.Goals ?? new()).OrderBy(g => g.Sequence))
        {
            var team = NormalizeTeam(g.Team)!;
            if (team == Black) black++; else white++;
            var assists = g.Assists ?? new List<PlayerKeyRef>();
            yield return new GameGoal
            {
                GameResultId = result.Id,
                Sequence = g.Sequence,
                Team = team,
                Period = g.Period,
                PeriodElapsedSec = g.PeriodElapsedSec,
                BlackScoreAfter = black,
                WhiteScoreAfter = white,
                ScorerLineId = g.Scorer != null ? lines[g.Scorer.PlayerKey].Id : null,
                ScorerSource = g.Scorer != null ? (g.ScorerSource ?? "person_click") : null,
                PrimaryAssistLineId = assists.Count > 0 ? lines[assists[0].PlayerKey].Id : null,
                SecondaryAssistLineId = assists.Count > 1 ? lines[assists[1].PlayerKey].Id : null,
                AssistsSource = g.AssistsSource,
                VideoOffsetSec = g.VideoOffsetSec,
            };
        }
    }

    /// <summary>SHA-256 over the body as re-serialized (stable property order, Web defaults).</summary>
    public static string PayloadHash(GameResultUpsertRequest r)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r, Json));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string? NormalizeTeam(string? t) =>
        t?.Trim().ToLowerInvariant() switch { "black" => Black, "white" => White, _ => null };

    private static string? NormalizePosition(string? p) =>
        p?.Trim().ToLowerInvariant() switch { "skater" => "Skater", "goalie" => "Goalie", _ => null };

    // =====================================================================
    // READ
    // =====================================================================

    public async Task<GameResultDto?> GetResultAsync(Guid eventId, Guid? callerId)
    {
        if (!_options.ReadsEnabled) return null;

        var result = await _context.GameResults.AsNoTracking()
            .Include(r => r.PlayerLines).ThenInclude(l => l.User)
            .Include(r => r.Goals)
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.Status == GameResultStatus.Published);
        if (result == null) return null;

        // The event's own visibility rules, through their one owner
        if (!await _visibility.CanSeeAsync(eventId, callerId)) return null;

        var byId = result.PlayerLines.ToDictionary(l => l.Id);
        GamePlayerRefDto? Ref(Guid? lineId) => lineId.HasValue && byId.TryGetValue(lineId.Value, out var l) ? RefOf(l) : null;

        var goals = result.Goals.OrderBy(g => g.Sequence).Select(g => new GameGoalDto(
            g.Sequence, g.Team, g.Period, g.PeriodElapsedSec, g.BlackScoreAfter, g.WhiteScoreAfter,
            Ref(g.ScorerLineId),
            new[] { g.PrimaryAssistLineId, g.SecondaryAssistLineId }.Select(Ref).Where(x => x != null).Select(x => x!).ToList(),
            g.AssistsSource == PersonRuled,
            g.VideoOffsetSec)).ToList();

        bool Hidden(GamePlayerLine l) => l.User != null && l.User.HideGameStats;

        var stars = result.PlayerLines.Where(l => l.Star.HasValue && (GameResultSettings.HiddenPlayersKeepNamesInRecap || !Hidden(l)))
            .OrderBy(l => l.Star).Select(LineOf).ToList();
        List<GamePlayerLineDto> Side(string team) => result.PlayerLines
            .Where(l => l.Team == team && !Hidden(l))
            .OrderBy(l => l.Position == "Goalie" ? 1 : 0)
            .ThenByDescending(l => l.Points ?? -1).ThenByDescending(l => l.Goals ?? -1)
            .ThenBy(l => l.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(LineOf).ToList();

        return new GameResultDto(
            result.EventId,
            result.Status,
            new GameResultSideDto(result.BlackLabel, result.BlackScore),
            new GameResultSideDto(result.WhiteLabel, result.WhiteScore),
            result.BlackScore > result.WhiteScore ? Black : result.WhiteScore > result.BlackScore ? White : null,
            result.PeriodCount,
            result.VideoUrl,
            ReadJson<List<VideoChapterDto>>(result.VideoChaptersJson) ?? new(),
            goals,
            stars,
            Side(Black),
            Side(White),
            ReadJson<List<string>>(result.NotesJson) ?? new(),
            result.EstimateDisclaimer,
            result.UpdatedAt);
    }

    private static GamePlayerRefDto RefOf(GamePlayerLine l) => new(l.UserId, l.DisplayName, l.Jersey, l.IsLoaner);

    private static GamePlayerLineDto LineOf(GamePlayerLine l) => new(
        RefOf(l), l.Team, l.Position, l.Goals, l.Assists, l.Points, l.Star, l.GoalsAgainst, l.Saves,
        ReadJson<Dictionary<string, GameEstimateDto>>(l.EstimatesJson));

    private static T? ReadJson<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch (JsonException) { return null; } // a stored blob that no longer parses reads as absent, never a 500
    }

    /// <summary>Organizations the caller belongs to: subscriptions and admin seats.</summary>
    private async Task<HashSet<Guid>> MemberOrgIdsAsync(Guid userId)
    {
        var subs = await _context.OrganizationSubscriptions.Where(s => s.UserId == userId).Select(s => s.OrganizationId).ToListAsync();
        var admins = await _context.OrganizationAdmins.Where(a => a.UserId == userId).Select(a => a.OrganizationId).ToListAsync();
        return subs.Concat(admins).ToHashSet();
    }

    private record LineRow(GamePlayerLine Line, GameResult Result, DateTime EventDate, bool HasUnknownScorer);

    private async Task<List<LineRow>> PublishedLinesAsync(IQueryable<GamePlayerLine> lines)
    {
        var rows = await lines
            .Where(l => l.GameResult.Status == GameResultStatus.Published)
            .Select(l => new
            {
                Line = l,
                Result = l.GameResult,
                l.GameResult.Event.EventDate,
                Unknown = l.GameResult.Goals.Any(g => g.ScorerLineId == null),
            })
            .AsNoTracking()
            .ToListAsync();
        return rows.Select(x => new LineRow(x.Line, x.Result, x.EventDate, x.Unknown)).ToList();
    }

    public static string SeasonOf(DateTime eventDateUtc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(eventDateUtc, DateTimeKind.Utc), GameResultSettings.SeasonTimeZone)
            .Year.ToString();

    private static string ParseSeason(string? season)
    {
        if (string.IsNullOrWhiteSpace(season)) return SeasonOf(DateTime.UtcNow);
        if (season.Length == 4 && int.TryParse(season, out var y) && y is >= 2000 and <= 2100) return season;
        throw new InvalidOperationException("season must be a four-digit year");
    }

    private static PlayerSeasonLineDto? Totals(IReadOnlyCollection<LineRow> rows)
    {
        if (rows.Count == 0) return null;
        var goalies = rows.Where(r => r.Line.Position == "Goalie").ToList();
        return new PlayerSeasonLineDto(
            rows.Count,
            rows.Sum(r => r.Line.Goals ?? 0),
            rows.Sum(r => r.Line.Assists ?? 0),
            rows.Sum(r => r.Line.Points ?? (r.Line.Goals ?? 0) + (r.Line.Assists ?? 0)),
            rows.Count(r => r.Line.Star.HasValue),
            goalies.Count > 0 && goalies.All(g => g.Line.GoalsAgainst.HasValue) ? goalies.Sum(g => g.Line.GoalsAgainst!.Value) : null,
            rows.Count(r => r.HasUnknownScorer));
    }

    public async Task<PlayerStatsDto?> GetPlayerStatsAsync(Guid userId, string? season, Guid callerId)
    {
        if (!_options.ReadsEnabled) return null;
        var seasonKey = ParseSeason(season);

        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive && !u.IsGhostPlayer);
        if (user == null) return null;

        var isSelf = userId == callerId;
        var callerOrgs = isSelf ? null : await MemberOrgIdsAsync(callerId);

        var rows = await PublishedLinesAsync(_context.GamePlayerLines.Where(l => l.UserId == userId));
        if (!isSelf)
        {
            // Only games of organizations the caller belongs to; and the player must
            // share at least one organization with the caller (hides existence: 404).
            var targetOrgs = await MemberOrgIdsAsync(userId);
            targetOrgs.UnionWith(rows.Select(r => r.Result.OrganizationId));
            if (!targetOrgs.Overlaps(callerOrgs!)) return null;
            rows = rows.Where(r => callerOrgs!.Contains(r.Result.OrganizationId)).ToList();
        }

        if (user.HideGameStats && !isSelf)
        {
            return new PlayerStatsDto(user.Id, user.FirstName, user.LastName, true, seasonKey, null, null, new());
        }

        var inSeason = rows.Where(r => SeasonOf(r.EventDate) == seasonKey).ToList();
        var log = inSeason.OrderByDescending(r => r.EventDate).Select(r =>
        {
            var mine = r.Line.Team;
            var teamScore = mine == Black ? r.Result.BlackScore : r.Result.WhiteScore;
            var oppScore = mine == Black ? r.Result.WhiteScore : r.Result.BlackScore;
            return new PlayerGameLogEntryDto(
                r.Result.EventId, r.EventDate,
                mine == Black ? r.Result.WhiteLabel : r.Result.BlackLabel,
                mine,
                teamScore > oppScore ? "W" : teamScore < oppScore ? "L" : "T",
                teamScore, oppScore,
                r.Line.Goals, r.Line.Assists, r.Line.Points, r.Line.Star,
                ReadJson<Dictionary<string, GameEstimateDto>>(r.Line.EstimatesJson));
        }).ToList();

        return new PlayerStatsDto(user.Id, user.FirstName, user.LastName, false, seasonKey,
            Totals(inSeason), Totals(rows), log)
        {
            GamesWithoutStats = await GamesWithoutStatsAsync(userId, seasonKey, log.Select(g => g.EventId).ToHashSet(),
                isSelf ? null : callerOrgs, callerId),
        };
    }

    /// <summary>
    /// The season's past organization games the player was registered for that have no
    /// line in the log. Read-only over existing tables; each event must pass the event
    /// visibility rules for the caller (an invite-only game stays out for those not invited).
    /// </summary>
    private async Task<List<PlayerGameWithoutStatsDto>> GamesWithoutStatsAsync(
        Guid userId, string seasonKey, HashSet<Guid> inLog, HashSet<Guid>? callerOrgs, Guid callerId)
    {
        var now = DateTime.UtcNow;
        var regs = await _context.EventRegistrations.AsNoTracking()
            .Where(r => r.UserId == userId && r.Status == "Registered"
                        && r.Event.OrganizationId != null
                        && r.Event.EventDate < now
                        && r.Event.Status != "Cancelled" && r.Event.Status != "Draft")
            .Select(r => new
            {
                r.EventId, r.Event.EventDate, r.Event.Name, OrgId = r.Event.OrganizationId!.Value,
                r.TeamAssignment, r.Event.IsRosterPublished,
            })
            .ToListAsync();

        var list = new List<PlayerGameWithoutStatsDto>();
        foreach (var r in regs.Where(r => !inLog.Contains(r.EventId) && SeasonOf(r.EventDate) == seasonKey)
                     .OrderByDescending(r => r.EventDate).Take(GameResultSettings.GamesWithoutStatsMax))
        {
            if (callerOrgs != null && !callerOrgs.Contains(r.OrgId)) continue;
            if (!await _visibility.CanSeeAsync(r.EventId, callerId)) continue;
            var team = r.IsRosterPublished && (r.TeamAssignment == Black || r.TeamAssignment == White) ? r.TeamAssignment : null;
            list.Add(new PlayerGameWithoutStatsDto(r.EventId, r.EventDate, r.Name, team));
        }
        return list;
    }

    private async Task RequireMemberAsync(Guid organizationId, Guid callerId)
    {
        var member = await _context.OrganizationSubscriptions.AnyAsync(s => s.OrganizationId == organizationId && s.UserId == callerId)
                     || await _adminService.IsUserAdminAsync(organizationId, callerId);
        if (!member)
        {
            _logger.LogWarning("Stats read denied for organization {OrganizationId}: {UserId} is not a member", organizationId, callerId);
            throw new UnauthorizedAccessException("Only members of the organization can see its stats");
        }
    }

    public async Task<LeaderboardDto?> GetLeaderboardAsync(Guid organizationId, string? season, string? stat, Guid callerId)
    {
        if (!_options.ReadsEnabled) return null;
        var statKey = string.IsNullOrWhiteSpace(stat) ? "points" : stat.Trim().ToLowerInvariant();
        if (!GameResultSettings.LeaderboardStats.Contains(statKey))
            throw new InvalidOperationException("stat must be points, goals, assists or stars");
        var seasonKey = ParseSeason(season);
        if (!await _context.Organizations.AnyAsync(o => o.Id == organizationId)) return null;
        await RequireMemberAsync(organizationId, callerId);

        var results = await _context.GameResults.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.Status == GameResultStatus.Published)
            .Select(r => new { r.Id, r.Event.EventDate }).ToListAsync();
        var seasonIds = results.Where(r => SeasonOf(r.EventDate) == seasonKey).Select(r => r.Id).ToHashSet();

        var rows = (await PublishedLinesAsync(_context.GamePlayerLines.Where(l =>
                l.UserId != null && l.GameResult.OrganizationId == organizationId
                && l.User!.IsActive && !l.User.IsGhostPlayer && !l.User.HideGameStats)))
            .Where(r => seasonIds.Contains(r.Result.Id)).ToList();

        var userIds = rows.Select(r => r.Line.UserId!.Value).Distinct().ToList();
        var names = await _context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FirstName, u.LastName });

        var totals = rows.GroupBy(r => r.Line.UserId!.Value).Select(g =>
        {
            var t = Totals(g.ToList())!;
            var n = names[g.Key];
            return new { UserId = g.Key, n.FirstName, n.LastName, t };
        }).ToList();

        int Value(PlayerSeasonLineDto t) => statKey switch
        {
            "goals" => t.Goals, "assists" => t.Assists, "stars" => t.Stars, _ => t.Points,
        };

        var ordered = totals
            .OrderByDescending(x => Value(x.t)).ThenByDescending(x => x.t.Points).ThenByDescending(x => x.t.Goals)
            .ThenBy(x => x.LastName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.FirstName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var list = new List<LeaderboardRowDto>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var x = ordered[i];
            var rank = i > 0 && Value(ordered[i - 1].t) == Value(x.t) ? list[i - 1].Rank : i + 1;
            list.Add(new LeaderboardRowDto(rank, x.UserId, x.FirstName, x.LastName,
                x.t.GamesWithStats, x.t.Goals, x.t.Assists, x.t.Points, x.t.Stars));
        }
        return new LeaderboardDto(organizationId, seasonKey, statKey, seasonIds.Count, list);
    }

    public async Task<List<GameResultSummaryDto>?> GetRecentResultsAsync(Guid organizationId, int limit, Guid callerId)
    {
        if (!_options.ReadsEnabled) return null;
        if (!await _context.Organizations.AnyAsync(o => o.Id == organizationId)) return null;
        await RequireMemberAsync(organizationId, callerId);
        var take = Math.Clamp(limit, 1, GameResultSettings.RecentResultsMax);

        return await _context.GameResults.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.Status == GameResultStatus.Published
                        && r.Event.Status != "Cancelled")
            .OrderByDescending(r => r.Event.EventDate)
            .Take(take)
            .Select(r => new GameResultSummaryDto(r.EventId, r.Event.EventDate, r.Event.Name,
                new GameResultSideDto(r.BlackLabel, r.BlackScore),
                new GameResultSideDto(r.WhiteLabel, r.WhiteScore)))
            .ToListAsync();
    }

    public async Task<StatsVisibilityDto?> GetStatsVisibilityAsync(Guid callerId)
    {
        // The read switch hides the Profile tab's Game Stats block too (404 -> the app shows nothing)
        if (!_options.ReadsEnabled) return null;
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == callerId);
        return user == null ? null : new StatsVisibilityDto(user.HideGameStats);
    }

    public async Task<StatsVisibilityDto?> SetStatsVisibilityAsync(Guid callerId, bool hidden)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == callerId);
        if (user == null) return null;
        if (user.HideGameStats != hidden)
        {
            user.HideGameStats = hidden;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        return new StatsVisibilityDto(user.HideGameStats);
    }
}
