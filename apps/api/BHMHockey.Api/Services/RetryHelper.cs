using BHMHockey.Api.Models.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

/// <summary>
/// Provides retry logic for database operations that may encounter concurrent modifications.
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// Executes an operation with automatic retry on DbUpdateConcurrencyException.
    /// Uses linear backoff with delays of 100ms, 200ms, 300ms between retries.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3).</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ConcurrentModificationException">
    /// Thrown when all retry attempts are exhausted due to concurrent modifications.
    /// </exception>
    public static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (attempt == maxRetries - 1)
                {
                    throw new ConcurrentModificationException(
                        "The operation failed due to a concurrent modification. Please try again.",
                        ex);
                }

                // Linear backoff: 100ms, 200ms, 300ms
                await Task.Delay(100 * (attempt + 1));
            }
        }

        // This should never be reached due to the throw in the catch block
        throw new InvalidOperationException("Max retries exceeded");
    }

    /// <summary>
    /// Executes a void operation with automatic retry on DbUpdateConcurrencyException.
    /// Uses linear backoff with delays of 100ms, 200ms, 300ms between retries.
    /// </summary>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3).</param>
    /// <exception cref="ConcurrentModificationException">
    /// Thrown when all retry attempts are exhausted due to concurrent modifications.
    /// </exception>
    public static async Task ExecuteWithRetryAsync(Func<Task> operation, int maxRetries = 3)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true;
        }, maxRetries);
    }
}
