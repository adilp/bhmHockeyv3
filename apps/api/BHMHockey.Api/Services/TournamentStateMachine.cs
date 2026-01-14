namespace BHMHockey.Api.Services;

/// <summary>
/// State machine for tournament lifecycle transitions.
/// Defines valid state transitions and provides validation.
/// </summary>
public static class TournamentStateMachine
{
    /// <summary>
    /// Valid state transitions: FromStatus -> Set of valid ToStatuses
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> ValidTransitions = new()
    {
        ["Draft"] = new() { "Open", "Cancelled" },
        ["Open"] = new() { "RegistrationClosed", "Cancelled" },
        ["RegistrationClosed"] = new() { "InProgress", "Cancelled" },
        ["InProgress"] = new() { "Completed", "Postponed", "Cancelled" },
        ["Postponed"] = new() { "InProgress", "Cancelled" },
        ["Completed"] = new(), // Terminal state - no transitions out
        ["Cancelled"] = new()  // Terminal state - no transitions out
    };

    /// <summary>
    /// Maps transition to the action name for audit logging.
    /// Key format: "FromStatus->ToStatus"
    /// </summary>
    private static readonly Dictionary<string, string> TransitionActions = new()
    {
        ["Draft->Open"] = "Publish",
        ["Draft->Cancelled"] = "Cancel",
        ["Open->RegistrationClosed"] = "CloseRegistration",
        ["Open->Cancelled"] = "Cancel",
        ["RegistrationClosed->InProgress"] = "Start",
        ["RegistrationClosed->Cancelled"] = "Cancel",
        ["InProgress->Completed"] = "Complete",
        ["InProgress->Postponed"] = "Postpone",
        ["InProgress->Cancelled"] = "Cancel",
        ["Postponed->InProgress"] = "Resume",
        ["Postponed->Cancelled"] = "Cancel"
    };

    /// <summary>
    /// Checks if a transition from one status to another is valid.
    /// </summary>
    /// <param name="from">Current status</param>
    /// <param name="to">Target status</param>
    /// <returns>True if transition is valid, false otherwise</returns>
    public static bool CanTransition(string from, string to)
    {
        return ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    /// <summary>
    /// Validates a transition and throws if invalid.
    /// </summary>
    /// <param name="from">Current status</param>
    /// <param name="to">Target status</param>
    /// <exception cref="InvalidOperationException">Thrown when transition is not valid</exception>
    public static void ValidateTransition(string from, string to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Cannot transition tournament from '{from}' to '{to}'");
        }
    }

    /// <summary>
    /// Gets the action name for a given transition (for audit logging).
    /// </summary>
    /// <param name="from">Current status</param>
    /// <param name="to">Target status</param>
    /// <returns>Action name (e.g., "Publish", "Start", "Cancel")</returns>
    public static string GetActionForTransition(string from, string to)
    {
        var key = $"{from}->{to}";
        return TransitionActions.TryGetValue(key, out var action) ? action : "Unknown";
    }
}
