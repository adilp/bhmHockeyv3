using BHMHockey.Api.Data;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for TournamentLifecycleService - TDD approach for TRN-002.
/// Tests written FIRST before implementation.
/// </summary>
public class TournamentLifecycleServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TournamentService _tournamentService;
    private readonly TournamentLifecycleService _sut;
    private readonly TournamentAuthorizationService _authService;

    public TournamentLifecycleServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _authService = new TournamentAuthorizationService(_context);
        var orgAdminService = new OrganizationAdminService(_context);
        _tournamentService = new TournamentService(_context, orgAdminService, _authService);
        _sut = new TournamentLifecycleService(_context, _tournamentService, _authService);
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

    #endregion

    #region PublishAsync Tests

    [Fact]
    public async Task PublishAsync_ValidDraftTournament_TransitionsToOpen()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");

        // Act
        var result = await _sut.PublishAsync(tournament.Id, user.Id);

        // Assert
        result.Status.Should().Be("Open");
    }

    [Fact]
    public async Task PublishAsync_ValidDraftTournament_SetsPublishedAtTimestamp()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        var beforePublish = DateTime.UtcNow;

        // Act
        var result = await _sut.PublishAsync(tournament.Id, user.Id);

        // Assert
        result.PublishedAt.Should().NotBeNull();
        result.PublishedAt.Should().BeOnOrAfter(beforePublish);
    }

    [Fact]
    public async Task PublishAsync_ValidDraftTournament_CreatesAuditLog()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");

        // Act
        await _sut.PublishAsync(tournament.Id, user.Id);

        // Assert
        var auditLog = await _context.TournamentAuditLogs
            .FirstOrDefaultAsync(a => a.TournamentId == tournament.Id);
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("Publish");
        auditLog.FromStatus.Should().Be("Draft");
        auditLog.ToStatus.Should().Be("Open");
        auditLog.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task PublishAsync_TournamentNotInDraftStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open"); // Already Open

        // Act
        var act = () => _sut.PublishAsync(tournament.Id, user.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot transition*Open*");
    }

    [Fact]
    public async Task PublishAsync_TournamentNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();

        // Act
        var act = () => _sut.PublishAsync(Guid.NewGuid(), user.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task PublishAsync_UserNotAdmin_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var creator = await CreateTestUser("creator@test.com");
        var otherUser = await CreateTestUser("other@test.com");
        var tournament = await CreateTestTournament(creator.Id, "Draft");

        // Act
        var act = () => _sut.PublishAsync(tournament.Id, otherUser.Id);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region CloseRegistrationAsync Tests

    [Fact]
    public async Task CloseRegistrationAsync_ValidOpenTournament_TransitionsToRegistrationClosed()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");

        // Act
        var result = await _sut.CloseRegistrationAsync(tournament.Id, user.Id);

        // Assert
        result.Status.Should().Be("RegistrationClosed");
    }

    [Fact]
    public async Task CloseRegistrationAsync_ValidOpenTournament_CreatesAuditLog()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");

        // Act
        await _sut.CloseRegistrationAsync(tournament.Id, user.Id);

        // Assert
        var auditLog = await _context.TournamentAuditLogs
            .FirstOrDefaultAsync(a => a.TournamentId == tournament.Id);
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("CloseRegistration");
        auditLog.FromStatus.Should().Be("Open");
        auditLog.ToStatus.Should().Be("RegistrationClosed");
    }

    [Fact]
    public async Task CloseRegistrationAsync_TournamentNotOpen_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");

        // Act
        var act = () => _sut.CloseRegistrationAsync(tournament.Id, user.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot transition*Draft*");
    }

    #endregion

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_ValidRegistrationClosedTournament_TransitionsToInProgress()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "RegistrationClosed");

        // Act
        var result = await _sut.StartAsync(tournament.Id, user.Id);

        // Assert
        result.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task StartAsync_ValidRegistrationClosedTournament_SetsStartedAtTimestamp()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "RegistrationClosed");
        var beforeStart = DateTime.UtcNow;

        // Act
        var result = await _sut.StartAsync(tournament.Id, user.Id);

        // Assert
        result.StartedAt.Should().NotBeNull();
        result.StartedAt.Should().BeOnOrAfter(beforeStart);
    }

    [Fact]
    public async Task StartAsync_ValidRegistrationClosedTournament_CreatesAuditLog()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "RegistrationClosed");

        // Act
        await _sut.StartAsync(tournament.Id, user.Id);

        // Assert
        var auditLog = await _context.TournamentAuditLogs
            .FirstOrDefaultAsync(a => a.TournamentId == tournament.Id);
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("Start");
        auditLog.FromStatus.Should().Be("RegistrationClosed");
        auditLog.ToStatus.Should().Be("InProgress");
    }

    [Fact]
    public async Task StartAsync_TournamentNotRegistrationClosed_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");

        // Act
        var act = () => _sut.StartAsync(tournament.Id, user.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot transition*Open*");
    }

    #endregion

    #region CompleteAsync Tests

    [Fact]
    public async Task CompleteAsync_ValidInProgressTournament_TransitionsToCompleted()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "InProgress");

        // Act
        var result = await _sut.CompleteAsync(tournament.Id, user.Id);

        // Assert
        result.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task CompleteAsync_ValidInProgressTournament_SetsCompletedAtTimestamp()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "InProgress");
        var beforeComplete = DateTime.UtcNow;

        // Act
        var result = await _sut.CompleteAsync(tournament.Id, user.Id);

        // Assert
        result.CompletedAt.Should().NotBeNull();
        result.CompletedAt.Should().BeOnOrAfter(beforeComplete);
    }

    [Fact]
    public async Task CompleteAsync_ValidInProgressTournament_CreatesAuditLog()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "InProgress");

        // Act
        await _sut.CompleteAsync(tournament.Id, user.Id);

        // Assert
        var auditLog = await _context.TournamentAuditLogs
            .FirstOrDefaultAsync(a => a.TournamentId == tournament.Id);
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("Complete");
        auditLog.FromStatus.Should().Be("InProgress");
        auditLog.ToStatus.Should().Be("Completed");
    }

    [Fact]
    public async Task CompleteAsync_TournamentNotInProgress_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");

        // Act
        var act = () => _sut.CompleteAsync(tournament.Id, user.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot transition*Open*");
    }

    #endregion

    #region PostponeAsync Tests

    [Fact]
    public async Task PostponeAsync_ValidInProgressTournament_TransitionsToPostponed()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "InProgress");

        // Act
        var result = await _sut.PostponeAsync(tournament.Id, user.Id);

        // Assert
        result.Status.Should().Be("Postponed");
    }

    [Fact]
    public async Task PostponeAsync_WithNewDates_UpdatesPostponedToDate()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "InProgress");
        var newStartDate = DateTime.UtcNow.AddDays(60);
        var newEndDate = DateTime.UtcNow.AddDays(62);

        // Act
        var result = await _sut.PostponeAsync(tournament.Id, user.Id, newStartDate, newEndDate);

        // Assert
        result.PostponedToDate.Should().Be(newStartDate);
        result.StartDate.Should().Be(newStartDate);
        result.EndDate.Should().Be(newEndDate);
    }

    [Fact]
    public async Task PostponeAsync_ValidInProgressTournament_CreatesAuditLog()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "InProgress");

        // Act
        await _sut.PostponeAsync(tournament.Id, user.Id);

        // Assert
        var auditLog = await _context.TournamentAuditLogs
            .FirstOrDefaultAsync(a => a.TournamentId == tournament.Id);
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("Postpone");
        auditLog.FromStatus.Should().Be("InProgress");
        auditLog.ToStatus.Should().Be("Postponed");
    }

    [Fact]
    public async Task PostponeAsync_TournamentNotInProgress_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");

        // Act
        var act = () => _sut.PostponeAsync(tournament.Id, user.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot transition*Open*");
    }

    #endregion

    #region ResumeAsync Tests

    [Fact]
    public async Task ResumeAsync_ValidPostponedTournament_TransitionsToInProgress()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Postponed");

        // Act
        var result = await _sut.ResumeAsync(tournament.Id, user.Id);

        // Assert
        result.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task ResumeAsync_ValidPostponedTournament_CreatesAuditLog()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Postponed");

        // Act
        await _sut.ResumeAsync(tournament.Id, user.Id);

        // Assert
        var auditLog = await _context.TournamentAuditLogs
            .FirstOrDefaultAsync(a => a.TournamentId == tournament.Id);
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("Resume");
        auditLog.FromStatus.Should().Be("Postponed");
        auditLog.ToStatus.Should().Be("InProgress");
    }

    [Fact]
    public async Task ResumeAsync_TournamentNotPostponed_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "InProgress");

        // Act
        var act = () => _sut.ResumeAsync(tournament.Id, user.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot transition*InProgress*");
    }

    #endregion

    #region CancelAsync Tests

    [Theory]
    [InlineData("Draft")]
    [InlineData("Open")]
    [InlineData("RegistrationClosed")]
    [InlineData("InProgress")]
    [InlineData("Postponed")]
    public async Task CancelAsync_FromAnyNonTerminalState_TransitionsToCancelled(string fromStatus)
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, fromStatus);

        // Act
        var result = await _sut.CancelAsync(tournament.Id, user.Id);

        // Assert
        result.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task CancelAsync_ValidTournament_SetsCancelledAtTimestamp()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        var beforeCancel = DateTime.UtcNow;

        // Act
        var result = await _sut.CancelAsync(tournament.Id, user.Id);

        // Assert
        result.CancelledAt.Should().NotBeNull();
        result.CancelledAt.Should().BeOnOrAfter(beforeCancel);
    }

    [Fact]
    public async Task CancelAsync_ValidTournament_CreatesAuditLog()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");

        // Act
        await _sut.CancelAsync(tournament.Id, user.Id);

        // Assert
        var auditLog = await _context.TournamentAuditLogs
            .FirstOrDefaultAsync(a => a.TournamentId == tournament.Id);
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("Cancel");
        auditLog.FromStatus.Should().Be("Open");
        auditLog.ToStatus.Should().Be("Cancelled");
    }

    [Fact]
    public async Task CancelAsync_CompletedTournament_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Completed");

        // Act
        var act = () => _sut.CancelAsync(tournament.Id, user.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot transition*Completed*");
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelledTournament_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Cancelled");

        // Act
        var act = () => _sut.CancelAsync(tournament.Id, user.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot transition*Cancelled*");
    }

    #endregion
}
