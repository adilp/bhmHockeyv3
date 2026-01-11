namespace BHMHockey.Api.Models.Exceptions;

/// <summary>
/// Thrown when a database operation fails due to concurrent modifications
/// after exhausting all retry attempts.
/// </summary>
public class ConcurrentModificationException : Exception
{
    public ConcurrentModificationException(string message) : base(message) { }

    public ConcurrentModificationException(string message, Exception innerException)
        : base(message, innerException) { }
}
