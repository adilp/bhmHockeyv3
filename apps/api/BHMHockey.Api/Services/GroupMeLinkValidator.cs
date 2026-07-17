namespace BHMHockey.Api.Services;

/// <summary>
/// Normalizes and validates GroupMe chat links for organizations and events.
/// Empty/whitespace input clears the link (returns null); anything else must be
/// an https URL on groupme.com (or www.groupme.com).
/// </summary>
public static class GroupMeLinkValidator
{
    private const int MaxLength = 500;
    private static readonly HashSet<string> ValidHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "groupme.com",
        "www.groupme.com"
    };

    /// <summary>
    /// Returns the trimmed link, or null when the input is null/empty/whitespace.
    /// Throws InvalidOperationException for links that aren't https GroupMe URLs.
    /// </summary>
    public static string? Normalize(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return null;

        var trimmed = link.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new InvalidOperationException($"GroupMe link must not exceed {MaxLength} characters.");
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !ValidHosts.Contains(uri.Host))
        {
            throw new InvalidOperationException("GroupMe link must be an https://groupme.com URL (e.g., https://groupme.com/join_group/...).");
        }

        return trimmed;
    }
}
