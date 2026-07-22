namespace BHMHockey.Api.Services;

/// <summary>
/// Canonicalizes a roster position to "Goalie" or "Skater", rejecting anything else.
/// Callers decide how to treat a missing position: organizer adds default to Skater,
/// while auto-roster requires an explicit choice (a null/blank position is invalid here).
/// </summary>
public static class PositionNormalizer
{
    public static string Normalize(string? position)
    {
        var normalized = position?.ToLowerInvariant();
        if (normalized != "goalie" && normalized != "skater")
        {
            throw new InvalidOperationException("Invalid position. Must be 'Goalie' or 'Skater'");
        }

        return normalized == "goalie" ? "Goalie" : "Skater";
    }
}
