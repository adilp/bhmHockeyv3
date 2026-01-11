using BHMHockey.Api.Models.Exceptions;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for RetryHelper - verifies retry behavior for concurrent modification handling.
/// </summary>
public class RetryHelperTests
{
    #region ExecuteWithRetryAsync<T> Tests

    [Fact]
    public async Task ExecuteWithRetryAsync_SuccessOnFirstAttempt_ReturnsResult()
    {
        // Arrange
        var expectedResult = "success";
        var attemptCount = 0;

        // Act
        var result = await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            return expectedResult;
        });

        // Assert
        result.Should().Be(expectedResult);
        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_SucceedsOnSecondAttempt_RetriesAndReturnsResult()
    {
        // Arrange
        var expectedResult = 42;
        var attemptCount = 0;

        // Act
        var result = await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new DbUpdateConcurrencyException("Simulated concurrency conflict");
            }
            await Task.CompletedTask;
            return expectedResult;
        });

        // Assert
        result.Should().Be(expectedResult);
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_SucceedsOnThirdAttempt_RetriesAndReturnsResult()
    {
        // Arrange
        var expectedResult = "third time's a charm";
        var attemptCount = 0;

        // Act
        var result = await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new DbUpdateConcurrencyException("Simulated concurrency conflict");
            }
            await Task.CompletedTask;
            return expectedResult;
        });

        // Assert
        result.Should().Be(expectedResult);
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_FailsAllAttempts_ThrowsConcurrentModificationException()
    {
        // Arrange
        var attemptCount = 0;

        // Act
        var act = () => RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new DbUpdateConcurrencyException("Persistent concurrency conflict");
            return "never reached";
        });

        // Assert
        await act.Should().ThrowAsync<ConcurrentModificationException>()
            .WithMessage("*concurrent modification*");
        attemptCount.Should().Be(3); // Default maxRetries
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_NonConcurrencyException_DoesNotRetry()
    {
        // Arrange
        var attemptCount = 0;

        // Act
        var act = () => RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new InvalidOperationException("Business rule violation");
            return "never reached";
        });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Business rule violation");
        attemptCount.Should().Be(1); // Should not retry
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WithCustomMaxRetries_RespectsLimit()
    {
        // Arrange
        var attemptCount = 0;
        var maxRetries = 5;

        // Act
        var act = () => RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new DbUpdateConcurrencyException("Persistent conflict");
            return "never reached";
        }, maxRetries);

        // Assert
        await act.Should().ThrowAsync<ConcurrentModificationException>();
        attemptCount.Should().Be(5);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_PreservesInnerException()
    {
        // Arrange
        var originalException = new DbUpdateConcurrencyException("Original error");

        // Act
        var act = () => RetryHelper.ExecuteWithRetryAsync<string>(async () =>
        {
            await Task.CompletedTask;
            throw originalException;
        });

        // Assert
        var exception = await act.Should().ThrowAsync<ConcurrentModificationException>();
        exception.Which.InnerException.Should().BeOfType<DbUpdateConcurrencyException>();
    }

    #endregion

    #region ExecuteWithRetryAsync (void) Tests

    [Fact]
    public async Task ExecuteWithRetryAsync_VoidOperation_SucceedsOnFirstAttempt()
    {
        // Arrange
        var attemptCount = 0;

        // Act
        await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
        });

        // Assert
        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_VoidOperation_RetriesOnConcurrencyException()
    {
        // Arrange
        var attemptCount = 0;

        // Act
        await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new DbUpdateConcurrencyException("Simulated conflict");
            }
            await Task.CompletedTask;
        });

        // Assert
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_VoidOperation_ThrowsAfterMaxRetries()
    {
        // Arrange
        var attemptCount = 0;

        // Act
        var act = () => RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new DbUpdateConcurrencyException("Persistent conflict");
        });

        // Assert
        await act.Should().ThrowAsync<ConcurrentModificationException>();
        attemptCount.Should().Be(3);
    }

    #endregion

    #region Timing Tests

    [Fact]
    public async Task ExecuteWithRetryAsync_AppliesBackoffDelays()
    {
        // Arrange
        var attemptCount = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var act = () => RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new DbUpdateConcurrencyException("Conflict");
            return "never";
        });

        // Assert
        await act.Should().ThrowAsync<ConcurrentModificationException>();
        stopwatch.Stop();

        // Should have delays: 100ms + 200ms + (no delay after last failure) = ~300ms minimum
        // Using 250ms to account for test timing variations
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(250);
    }

    #endregion
}
