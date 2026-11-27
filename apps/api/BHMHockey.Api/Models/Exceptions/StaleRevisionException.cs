namespace BHMHockey.Api.Models.Exceptions;

/// <summary>
/// A game result write whose revision is behind the stored one, or one revision
/// sent with two different bodies. Mapped to 409 Conflict; the sender must stop,
/// not retry.
/// </summary>
public class StaleRevisionException : Exception
{
    public int CurrentRevision { get; }

    public StaleRevisionException(string message, int currentRevision) : base(message)
    {
        CurrentRevision = currentRevision;
    }
}
