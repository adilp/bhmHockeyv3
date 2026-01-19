using BHMHockey.Api.Data;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for TournamentAuditService - TDD approach.
/// Tests written FIRST before implementation.
/// </summary>
public class TournamentAuditServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TournamentAuditService _sut;
    private readonly TournamentAuthorizationService _authService;

    public TournamentAuditServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _authService = new TournamentAuthorizationService(_context);
        var logger = new Mock<ILogger<TournamentAuditService>>();
        _sut = new TournamentAuditService(_context, _authService, logger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private async Task<User> CreateTestUser(string email = "test@example.com")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_password",
            FirstName = "John",
            LastName = "Doe",
            Role = "Player",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Tournament> CreateTestTournament(Guid creatorId, string status = "Draft")
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Name = "Test Tournament",
            Format = "SingleElimination",
            TeamFormation = "OrganizerAssigned",
            Status = status,
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(32),
            RegistrationDeadline = DateTime.UtcNow.AddDays(25),
            MaxTeams = 8,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Tournaments.Add(tournament);

        // Add creator as admin
        var admin = new TournamentAdmin
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            UserId = creatorId,
            Role = "Owner",
            AddedAt = DateTime.UtcNow
        };
        _context.TournamentAdmins.Add(admin);

        await _context.SaveChangesAsync();
        return tournament;
    }

    private async Task<TournamentAuditLog> CreateTestAuditLog(
        Guid tournamentId,
        Guid userId,
        string action,
        string? fromStatus = null,
        string? toStatus = null,
        DateTime? timestamp = null)
    {
        var auditLog = new TournamentAuditLog
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            UserId = userId,
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Timestamp = timestamp ?? DateTime.UtcNow
        };
        _context.TournamentAuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
        return auditLog;
    }

    #endregion

    #region LogAsync Tests

    [Fact]
    public async Task LogAsync_CreatesAuditLogEntry()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        // Act
        await _sut.LogAsync(
            tournament.Id,
            user.Id,
            "Publish",
            fromStatus: "Draft",
            toStatus: "Open");
        await _context.SaveChangesAsync();

        // Assert
        var auditLog = await _context.TournamentAuditLogs
            .FirstOrDefaultAsync(a => a.TournamentId == tournament.Id);
        auditLog.Should().NotBeNull();
    }

    [Fact]
    public async Task LogAsync_SetsAllFieldsCorrectly()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        var entityId = Guid.NewGuid();
        var beforeLog = DateTime.UtcNow;

        // Act
        await _sut.LogAsync(
            tournamentId: tournament.Id,
            userId: user.Id,
            action: "SetCaptain",
            entityType: "Team",
            entityId: entityId,
            oldValue: "{\"captainId\":null}",
            newValue: "{\"captainId\":\"abc123\"}",
            fromStatus: null,
            toStatus: null,
            details: "{\"teamName\":\"Blue Team\"}");
        await _context.SaveChangesAsync();

        // Assert
        var auditLog = await _context.TournamentAuditLogs
            .FirstOrDefaultAsync(a => a.TournamentId == tournament.Id);
        auditLog.Should().NotBeNull();
        auditLog!.TournamentId.Should().Be(tournament.Id);
        auditLog.UserId.Should().Be(user.Id);
        auditLog.Action.Should().Be("SetCaptain");
        auditLog.FromStatus.Should().BeNull();
        auditLog.ToStatus.Should().BeNull();
        auditLog.EntityType.Should().Be("Team");
        auditLog.EntityId.Should().Be(entityId);
        auditLog.OldValue.Should().Be("{\"captainId\":null}");
        auditLog.NewValue.Should().Be("{\"captainId\":\"abc123\"}");
        auditLog.Details.Should().Be("{\"teamName\":\"Blue Team\"}");
        auditLog.Timestamp.Should().BeOnOrAfter(beforeLog);
    }

    [Fact]
    public async Task LogAsync_HandlesNullOptionalFields()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        // Act
        await _sut.LogAsync(
            tournament.Id,
            user.Id,
            "Publish",
            fromStatus: "Draft",
            toStatus: "Open");
        await _context.SaveChangesAsync();

        // Assert
        var auditLog = await _context.TournamentAuditLogs
            .FirstOrDefaultAsync(a => a.TournamentId == tournament.Id);
        auditLog.Should().NotBeNull();
        auditLog!.EntityType.Should().BeNull();
        auditLog.EntityId.Should().BeNull();
        auditLog.OldValue.Should().BeNull();
        auditLog.NewValue.Should().BeNull();
        auditLog.Details.Should().BeNull();
    }

    #endregion

    #region GetAuditLogsAsync Tests

    [Fact]
    public async Task GetAuditLogsAsync_ReturnsAuditLogs_OrderedByTimestampDesc()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        var log1 = await CreateTestAuditLog(tournament.Id, user.Id, "Publish", "Draft", "Open", DateTime.UtcNow.AddMinutes(-10));
        var log2 = await CreateTestAuditLog(tournament.Id, user.Id, "Start", "Open", "InProgress", DateTime.UtcNow.AddMinutes(-5));
        var log3 = await CreateTestAuditLog(tournament.Id, user.Id, "Complete", "InProgress", "Completed", DateTime.UtcNow);

        // Act
        var result = await _sut.GetAuditLogsAsync(tournament.Id, user.Id);

        // Assert
        result.AuditLogs.Should().HaveCount(3);
        result.AuditLogs[0].Action.Should().Be("Complete"); // Most recent first
        result.AuditLogs[1].Action.Should().Be("Start");
        result.AuditLogs[2].Action.Should().Be("Publish"); // Oldest last
    }

    [Fact]
    public async Task GetAuditLogsAsync_ReturnsPaginatedResults()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        // Create 5 audit logs
        for (int i = 0; i < 5; i++)
        {
            await CreateTestAuditLog(tournament.Id, user.Id, $"Action{i}", timestamp: DateTime.UtcNow.AddMinutes(-i));
        }

        // Act - Get second page with limit 2
        var result = await _sut.GetAuditLogsAsync(tournament.Id, user.Id, offset: 2, limit: 2);

        // Assert
        result.AuditLogs.Should().HaveCount(2);
        result.AuditLogs[0].Action.Should().Be("Action2"); // Offset skips first 2
        result.AuditLogs[1].Action.Should().Be("Action3");
    }

    [Fact]
    public async Task GetAuditLogsAsync_ClampsLimitTo50()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        // Create 60 audit logs
        for (int i = 0; i < 60; i++)
        {
            await CreateTestAuditLog(tournament.Id, user.Id, $"Action{i}", timestamp: DateTime.UtcNow.AddMinutes(-i));
        }

        // Act - Request 100 but should get max 50
        var result = await _sut.GetAuditLogsAsync(tournament.Id, user.Id, limit: 100);

        // Assert
        result.AuditLogs.Should().HaveCount(50);
    }

    [Fact]
    public async Task GetAuditLogsAsync_FiltersByAction()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        await CreateTestAuditLog(tournament.Id, user.Id, "Publish", "Draft", "Open");
        await CreateTestAuditLog(tournament.Id, user.Id, "Start", "Open", "InProgress");
        await CreateTestAuditLog(tournament.Id, user.Id, "Publish", "Draft", "Open"); // Another Publish
        await CreateTestAuditLog(tournament.Id, user.Id, "Complete", "InProgress", "Completed");

        // Act
        var result = await _sut.GetAuditLogsAsync(tournament.Id, user.Id, actionFilter: "Publish");

        // Assert
        result.AuditLogs.Should().HaveCount(2);
        result.AuditLogs.Should().AllSatisfy(log => log.Action.Should().Be("Publish"));
    }

    [Fact]
    public async Task GetAuditLogsAsync_FiltersByDateRange()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        var baseDate = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        await CreateTestAuditLog(tournament.Id, user.Id, "Action1", timestamp: baseDate.AddDays(-2)); // Jan 13
        await CreateTestAuditLog(tournament.Id, user.Id, "Action2", timestamp: baseDate.AddDays(-1)); // Jan 14
        await CreateTestAuditLog(tournament.Id, user.Id, "Action3", timestamp: baseDate); // Jan 15
        await CreateTestAuditLog(tournament.Id, user.Id, "Action4", timestamp: baseDate.AddDays(1)); // Jan 16
        await CreateTestAuditLog(tournament.Id, user.Id, "Action5", timestamp: baseDate.AddDays(2)); // Jan 17

        // Act - Get logs from Jan 14 to Jan 16
        var result = await _sut.GetAuditLogsAsync(
            tournament.Id,
            user.Id,
            fromDate: baseDate.AddDays(-1),
            toDate: baseDate.AddDays(1));

        // Assert
        result.AuditLogs.Should().HaveCount(3);
        result.AuditLogs.Should().Contain(log => log.Action == "Action2");
        result.AuditLogs.Should().Contain(log => log.Action == "Action3");
        result.AuditLogs.Should().Contain(log => log.Action == "Action4");
    }

    [Fact]
    public async Task GetAuditLogsAsync_IncludesUserName()
    {
        // Arrange
        var user = await CreateTestUser("john@example.com");
        var tournament = await CreateTestTournament(user.Id);

        await CreateTestAuditLog(tournament.Id, user.Id, "Publish", "Draft", "Open");

        // Act
        var result = await _sut.GetAuditLogsAsync(tournament.Id, user.Id);

        // Assert
        result.AuditLogs.Should().HaveCount(1);
        result.AuditLogs[0].UserName.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetAuditLogsAsync_ReturnsCorrectTotalCount()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        // Create 15 audit logs
        for (int i = 0; i < 15; i++)
        {
            await CreateTestAuditLog(tournament.Id, user.Id, $"Action{i}", timestamp: DateTime.UtcNow.AddMinutes(-i));
        }

        // Act - Get first 10 with pagination
        var result = await _sut.GetAuditLogsAsync(tournament.Id, user.Id, limit: 10);

        // Assert
        result.AuditLogs.Should().HaveCount(10); // First page
        result.TotalCount.Should().Be(15); // Total count regardless of pagination
    }

    [Fact]
    public async Task GetAuditLogsAsync_ReturnsCorrectHasMore()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        // Create 15 audit logs
        for (int i = 0; i < 15; i++)
        {
            await CreateTestAuditLog(tournament.Id, user.Id, $"Action{i}", timestamp: DateTime.UtcNow.AddMinutes(-i));
        }

        // Act - Get first 10
        var resultWithMore = await _sut.GetAuditLogsAsync(tournament.Id, user.Id, limit: 10);

        // Act - Get next 10 (only 5 remaining)
        var resultNoMore = await _sut.GetAuditLogsAsync(tournament.Id, user.Id, offset: 10, limit: 10);

        // Assert
        resultWithMore.HasMore.Should().BeTrue(); // Has 5 more after first 10
        resultNoMore.HasMore.Should().BeFalse(); // No more after getting all 15
    }

    [Fact]
    public async Task GetAuditLogsAsync_ReturnsEmptyForNoLogs()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        // Don't create any audit logs

        // Act
        var result = await _sut.GetAuditLogsAsync(tournament.Id, user.Id);

        // Assert
        result.AuditLogs.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuditLogsAsync_FiltersOnlyForSpecificTournament()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament1 = await CreateTestTournament(user.Id);
        var tournament2 = await CreateTestTournament(user.Id);

        await CreateTestAuditLog(tournament1.Id, user.Id, "Publish", "Draft", "Open");
        await CreateTestAuditLog(tournament2.Id, user.Id, "Publish", "Draft", "Open");
        await CreateTestAuditLog(tournament1.Id, user.Id, "Start", "Open", "InProgress");

        // Act
        var result = await _sut.GetAuditLogsAsync(tournament1.Id, user.Id);

        // Assert
        result.AuditLogs.Should().HaveCount(2); // Only tournament1 logs
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAuditLogsAsync_CombinesAllFilters()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        var baseDate = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        // Create varied logs
        await CreateTestAuditLog(tournament.Id, user.Id, "Publish", timestamp: baseDate.AddDays(-2));
        await CreateTestAuditLog(tournament.Id, user.Id, "Start", timestamp: baseDate.AddDays(-1));
        await CreateTestAuditLog(tournament.Id, user.Id, "Publish", timestamp: baseDate); // Matches all filters
        await CreateTestAuditLog(tournament.Id, user.Id, "Publish", timestamp: baseDate.AddDays(1)); // Matches action but outside date
        await CreateTestAuditLog(tournament.Id, user.Id, "Complete", timestamp: baseDate); // Matches date but wrong action

        // Act - Filter by action="Publish" AND date range
        var result = await _sut.GetAuditLogsAsync(
            tournament.Id,
            user.Id,
            actionFilter: "Publish",
            fromDate: baseDate.AddDays(-1),
            toDate: baseDate,
            limit: 10);

        // Assert
        result.AuditLogs.Should().HaveCount(1);
        result.AuditLogs[0].Action.Should().Be("Publish");
        result.AuditLogs[0].Timestamp.Should().Be(baseDate);
    }

    [Fact]
    public async Task GetAuditLogsAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var admin = await CreateTestUser("admin@test.com");
        var otherUser = await CreateTestUser("other@test.com");
        var tournament = await CreateTestTournament(admin.Id);

        await CreateTestAuditLog(tournament.Id, admin.Id, "Publish", "Draft", "Open");

        // Act
        var act = () => _sut.GetAuditLogsAsync(tournament.Id, otherUser.Id);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not authorized*");
    }

    #endregion
}
