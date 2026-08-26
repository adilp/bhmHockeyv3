using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for OrganizationJoinRequestService - the approval flow behind private
/// organizations. These protect the rules that a denial blocks re-requesting but
/// stays approvable, that only org admins can decide, and that both sides are
/// notified even when the recipient has no push token.
/// </summary>
public class OrganizationJoinRequestServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly OrganizationAdminService _adminService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly OrganizationJoinRequestService _sut;

    public OrganizationJoinRequestServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options);
        _adminService = new OrganizationAdminService(_context);
        _mockNotificationService = new Mock<INotificationService>();
        _sut = new OrganizationJoinRequestService(
            _context,
            _adminService,
            _mockNotificationService.Object,
            Mock.Of<ILogger<OrganizationJoinRequestService>>());
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private async Task<User> CreateTestUser(
        string email = "test@example.com",
        string? pushToken = "ExponentPushToken[abc]",
        bool isGhostPlayer = false)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_password",
            FirstName = "Test",
            LastName = "User",
            Role = "Player",
            IsActive = true,
            PushToken = pushToken,
            IsGhostPlayer = isGhostPlayer,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Organization> CreateTestOrganization(Guid creatorId, bool isPrivate = true)
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = $"Org {Guid.NewGuid()}",
            CreatorId = creatorId,
            IsActive = true,
            IsPrivate = isPrivate,
            CreatedAt = DateTime.UtcNow
        };
        _context.Organizations.Add(org);
        _context.OrganizationAdmins.Add(new OrganizationAdmin
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            UserId = creatorId,
            AddedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return org;
    }

    private async Task AddAdmin(Guid organizationId, Guid userId)
    {
        _context.OrganizationAdmins.Add(new OrganizationAdmin
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            AddedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    private void VerifyNotified(Guid userId, string type, Times times)
    {
        _mockNotificationService.Verify(n => n.SendPushNotificationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object?>(),
            userId,
            type,
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>()), times);
    }

    #endregion

    #region RequestJoin Tests

    [Fact]
    public async Task RequestJoinAsync_CreatesPendingRequest()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);

        var outcome = await _sut.RequestJoinAsync(org.Id, joiner.Id);

        outcome.Should().Be(SubscribeOutcome.JoinRequestCreated);
        var request = await _context.OrganizationJoinRequests.SingleAsync();
        request.Status.Should().Be(OrganizationJoinRequestStatus.Pending);
        request.UserId.Should().Be(joiner.Id);
    }

    [Fact]
    public async Task RequestJoinAsync_NotifiesEveryOrgAdmin()
    {
        var admin = await CreateTestUser("admin@example.com");
        var secondAdmin = await CreateTestUser("admin2@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await AddAdmin(org.Id, secondAdmin.Id);

        await _sut.RequestJoinAsync(org.Id, joiner.Id);

        VerifyNotified(admin.Id, "join_request", Times.Once());
        VerifyNotified(secondAdmin.Id, "join_request", Times.Once());
        VerifyNotified(joiner.Id, "join_request", Times.Never());
    }

    [Fact]
    public async Task RequestJoinAsync_AdminWithoutPushToken_StillRecordsTheNotification()
    {
        // The in-app notification row is created from userId+type, so a missing
        // push token must not short-circuit the dispatch
        var admin = await CreateTestUser("admin@example.com", pushToken: null);
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);

        await _sut.RequestJoinAsync(org.Id, joiner.Id);

        _mockNotificationService.Verify(n => n.SendPushNotificationAsync(
            string.Empty,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object?>(),
            admin.Id,
            "join_request",
            org.Id,
            null), Times.Once());
    }

    [Fact]
    public async Task RequestJoinAsync_SkipsGhostPlayerAdmins()
    {
        var admin = await CreateTestUser("admin@example.com");
        var ghostAdmin = await CreateTestUser("ghost@example.com", pushToken: null, isGhostPlayer: true);
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await AddAdmin(org.Id, ghostAdmin.Id);

        await _sut.RequestJoinAsync(org.Id, joiner.Id);

        VerifyNotified(ghostAdmin.Id, "join_request", Times.Never());
        VerifyNotified(admin.Id, "join_request", Times.Once());
    }

    [Fact]
    public async Task RequestJoinAsync_WhenAlreadyPending_ReportsPendingAndKeepsOneRow()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);

        var outcome = await _sut.RequestJoinAsync(org.Id, joiner.Id);

        outcome.Should().Be(SubscribeOutcome.JoinRequestAlreadyPending);
        (await _context.OrganizationJoinRequests.CountAsync()).Should().Be(1);
        // No repeat spam for the admins
        VerifyNotified(admin.Id, "join_request", Times.Once());
    }

    [Fact]
    public async Task RequestJoinAsync_AfterDenial_Throws()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);
        await _sut.DenyAsync(org.Id, joiner.Id, admin.Id);

        var act = () => _sut.RequestJoinAsync(org.Id, joiner.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*declined*");
    }

    [Fact]
    public async Task RequestJoinAsync_AfterApprovalButNoLongerSubscribed_ReopensThePendingRequest()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);
        await _sut.ApproveAsync(org.Id, joiner.Id, admin.Id);

        // Admin removes them from the member list, leaving the approved row behind
        var subscription = await _context.OrganizationSubscriptions.SingleAsync();
        _context.OrganizationSubscriptions.Remove(subscription);
        await _context.SaveChangesAsync();

        var outcome = await _sut.RequestJoinAsync(org.Id, joiner.Id);

        outcome.Should().Be(SubscribeOutcome.JoinRequestCreated);
        var request = await _context.OrganizationJoinRequests.SingleAsync();
        request.Status.Should().Be(OrganizationJoinRequestStatus.Pending);
        request.DecidedAt.Should().BeNull();
        request.DecidedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task RequestJoinAsync_ForUnknownOrganization_Throws()
    {
        var joiner = await CreateTestUser("joiner@example.com");

        var act = () => _sut.RequestJoinAsync(Guid.NewGuid(), joiner.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Approve Tests

    [Fact]
    public async Task ApproveAsync_SubscribesTheUserAndStampsTheDecision()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);

        var result = await _sut.ApproveAsync(org.Id, joiner.Id, admin.Id);

        result.Should().BeTrue();
        var request = await _context.OrganizationJoinRequests.SingleAsync();
        request.Status.Should().Be(OrganizationJoinRequestStatus.Approved);
        request.DecidedAt.Should().NotBeNull();
        request.DecidedByUserId.Should().Be(admin.Id);
        (await _context.OrganizationSubscriptions
            .AnyAsync(s => s.OrganizationId == org.Id && s.UserId == joiner.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ApproveAsync_NotifiesTheRequester()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);

        await _sut.ApproveAsync(org.Id, joiner.Id, admin.Id);

        VerifyNotified(joiner.Id, "join_request_approved", Times.Once());
    }

    [Fact]
    public async Task ApproveAsync_RequesterWithoutPushToken_StillRecordsTheNotification()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com", pushToken: null);
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);

        await _sut.ApproveAsync(org.Id, joiner.Id, admin.Id);

        _mockNotificationService.Verify(n => n.SendPushNotificationAsync(
            string.Empty,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object?>(),
            joiner.Id,
            "join_request_approved",
            org.Id,
            null), Times.Once());
    }

    [Fact]
    public async Task ApproveAsync_AfterDenial_StillSubscribesTheUser()
    {
        // An accidental deny must be recoverable from the admin screen
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);
        await _sut.DenyAsync(org.Id, joiner.Id, admin.Id);

        var result = await _sut.ApproveAsync(org.Id, joiner.Id, admin.Id);

        result.Should().BeTrue();
        (await _context.OrganizationJoinRequests.SingleAsync()).Status
            .Should().Be(OrganizationJoinRequestStatus.Approved);
        (await _context.OrganizationSubscriptions
            .AnyAsync(s => s.OrganizationId == org.Id && s.UserId == joiner.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ApproveAsync_WhenAlreadyApproved_IsIdempotent()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);
        await _sut.ApproveAsync(org.Id, joiner.Id, admin.Id);

        var result = await _sut.ApproveAsync(org.Id, joiner.Id, admin.Id);

        result.Should().BeTrue();
        (await _context.OrganizationSubscriptions
            .CountAsync(s => s.OrganizationId == org.Id && s.UserId == joiner.Id)).Should().Be(1);
        VerifyNotified(joiner.Id, "join_request_approved", Times.Once());
    }

    [Fact]
    public async Task ApproveAsync_WithNoRequest_ReturnsFalse()
    {
        var admin = await CreateTestUser("admin@example.com");
        var stranger = await CreateTestUser("stranger@example.com");
        var org = await CreateTestOrganization(admin.Id);

        var result = await _sut.ApproveAsync(org.Id, stranger.Id, admin.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ApproveAsync_AsNonAdmin_Throws()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var outsider = await CreateTestUser("outsider@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);

        var act = () => _sut.ApproveAsync(org.Id, joiner.Id, outsider.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await _context.OrganizationSubscriptions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ApproveAsync_OfOwnRequest_DoesNotNotifyTheActingAdmin()
    {
        // An admin who is also the requester never gets notified about their own action
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        _context.OrganizationJoinRequests.Add(new OrganizationJoinRequest
        {
            OrganizationId = org.Id,
            UserId = admin.Id
        });
        await _context.SaveChangesAsync();

        await _sut.ApproveAsync(org.Id, admin.Id, admin.Id);

        VerifyNotified(admin.Id, "join_request_approved", Times.Never());
    }

    #endregion

    #region Deny Tests

    [Fact]
    public async Task DenyAsync_MarksDeniedAndNotifiesTheRequester()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);

        var result = await _sut.DenyAsync(org.Id, joiner.Id, admin.Id);

        result.Should().BeTrue();
        var request = await _context.OrganizationJoinRequests.SingleAsync();
        request.Status.Should().Be(OrganizationJoinRequestStatus.Denied);
        request.DecidedAt.Should().NotBeNull();
        request.DecidedByUserId.Should().Be(admin.Id);
        (await _context.OrganizationSubscriptions.CountAsync()).Should().Be(0);
        VerifyNotified(joiner.Id, "join_request_denied", Times.Once());
    }

    [Fact]
    public async Task DenyAsync_RequesterWithoutPushToken_StillRecordsTheNotification()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com", pushToken: null);
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);

        await _sut.DenyAsync(org.Id, joiner.Id, admin.Id);

        _mockNotificationService.Verify(n => n.SendPushNotificationAsync(
            string.Empty,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object?>(),
            joiner.Id,
            "join_request_denied",
            org.Id,
            null), Times.Once());
    }

    [Fact]
    public async Task DenyAsync_WithNoRequest_ReturnsFalse()
    {
        var admin = await CreateTestUser("admin@example.com");
        var stranger = await CreateTestUser("stranger@example.com");
        var org = await CreateTestOrganization(admin.Id);

        var result = await _sut.DenyAsync(org.Id, stranger.Id, admin.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DenyAsync_AsNonAdmin_Throws()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var outsider = await CreateTestUser("outsider@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);

        var act = () => _sut.DenyAsync(org.Id, joiner.Id, outsider.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await _context.OrganizationJoinRequests.SingleAsync()).Status
            .Should().Be(OrganizationJoinRequestStatus.Pending);
    }

    [Fact]
    public async Task DenyAsync_SkipsGhostPlayerRequesters()
    {
        var admin = await CreateTestUser("admin@example.com");
        var ghost = await CreateTestUser("ghost@example.com", pushToken: null, isGhostPlayer: true);
        var org = await CreateTestOrganization(admin.Id);
        _context.OrganizationJoinRequests.Add(new OrganizationJoinRequest
        {
            OrganizationId = org.Id,
            UserId = ghost.Id
        });
        await _context.SaveChangesAsync();

        await _sut.DenyAsync(org.Id, ghost.Id, admin.Id);

        VerifyNotified(ghost.Id, "join_request_denied", Times.Never());
    }

    #endregion

    #region GetRequests Tests

    [Fact]
    public async Task GetRequestsAsync_WithPendingFilter_ReturnsOnlyPendingWithRequesterNames()
    {
        var admin = await CreateTestUser("admin@example.com");
        var pendingUser = await CreateTestUser("pending@example.com");
        var deniedUser = await CreateTestUser("denied@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, pendingUser.Id);
        await _sut.RequestJoinAsync(org.Id, deniedUser.Id);
        await _sut.DenyAsync(org.Id, deniedUser.Id, admin.Id);

        var requests = await _sut.GetRequestsAsync(org.Id, admin.Id, OrganizationJoinRequestStatus.Pending);

        requests.Should().HaveCount(1);
        requests[0].UserId.Should().Be(pendingUser.Id);
        requests[0].FirstName.Should().Be("Test");
        requests[0].LastName.Should().Be("User");
        requests[0].RequestedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task GetRequestsAsync_WithNullStatus_ReturnsEveryRequest()
    {
        var admin = await CreateTestUser("admin@example.com");
        var pendingUser = await CreateTestUser("pending@example.com");
        var deniedUser = await CreateTestUser("denied@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, pendingUser.Id);
        await _sut.RequestJoinAsync(org.Id, deniedUser.Id);
        await _sut.DenyAsync(org.Id, deniedUser.Id, admin.Id);

        var requests = await _sut.GetRequestsAsync(org.Id, admin.Id, null);

        requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRequestsAsync_WithDeniedFilter_LetsAdminsRevisitDenials()
    {
        var admin = await CreateTestUser("admin@example.com");
        var deniedUser = await CreateTestUser("denied@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, deniedUser.Id);
        await _sut.DenyAsync(org.Id, deniedUser.Id, admin.Id);

        var requests = await _sut.GetRequestsAsync(org.Id, admin.Id, OrganizationJoinRequestStatus.Denied);

        requests.Should().HaveCount(1);
        requests[0].Status.Should().Be(OrganizationJoinRequestStatus.Denied);
        requests[0].DecidedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRequestsAsync_AsNonAdmin_Throws()
    {
        var admin = await CreateTestUser("admin@example.com");
        var outsider = await CreateTestUser("outsider@example.com");
        var org = await CreateTestOrganization(admin.Id);

        var act = () => _sut.GetRequestsAsync(org.Id, outsider.Id, OrganizationJoinRequestStatus.Pending);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetRequestsAsync_WithInvalidStatus_Throws()
    {
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);

        var act = () => _sut.GetRequestsAsync(org.Id, admin.Id, "Bogus");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetRequestsAsync_ExcludesOtherOrganizations()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var otherOrg = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(otherOrg.Id, joiner.Id);

        var requests = await _sut.GetRequestsAsync(org.Id, admin.Id, OrganizationJoinRequestStatus.Pending);

        requests.Should().BeEmpty();
    }

    #endregion

    #region ClearApprovedRequest Tests

    [Fact]
    public async Task ClearApprovedRequestAsync_RemovesApprovedRow()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);
        await _sut.ApproveAsync(org.Id, joiner.Id, admin.Id);

        await _sut.ClearApprovedRequestAsync(org.Id, joiner.Id);

        (await _context.OrganizationJoinRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ClearApprovedRequestAsync_LeavesDeniedRowInPlace()
    {
        var admin = await CreateTestUser("admin@example.com");
        var joiner = await CreateTestUser("joiner@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.RequestJoinAsync(org.Id, joiner.Id);
        await _sut.DenyAsync(org.Id, joiner.Id, admin.Id);

        await _sut.ClearApprovedRequestAsync(org.Id, joiner.Id);

        (await _context.OrganizationJoinRequests.SingleAsync()).Status
            .Should().Be(OrganizationJoinRequestStatus.Denied);
    }

    [Fact]
    public async Task ClearApprovedRequestAsync_WithNoRow_DoesNothing()
    {
        var admin = await CreateTestUser("admin@example.com");
        var stranger = await CreateTestUser("stranger@example.com");
        var org = await CreateTestOrganization(admin.Id);

        var act = () => _sut.ClearApprovedRequestAsync(org.Id, stranger.Id);

        await act.Should().NotThrowAsync();
    }

    #endregion
}
