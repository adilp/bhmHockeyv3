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
/// Tests for OrganizationAutoRosterService - protecting the org "regulars" list.
/// These tests ensure:
/// - Only org admins can view or manage the auto-roster
/// - Adds are restricted to current subscribers with valid positions, no duplicates
/// - New members append at the end of the order
/// - Reordering validates completeness and persists new sort orders
/// </summary>
public class OrganizationAutoRosterServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly OrganizationAdminService _adminService;
    private readonly OrganizationAutoRosterService _sut;

    public OrganizationAutoRosterServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _adminService = new OrganizationAdminService(_context);
        _sut = new OrganizationAutoRosterService(_context, _adminService, Mock.Of<ILogger<OrganizationAutoRosterService>>());
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private async Task<User> CreateTestUser(string email = "user@example.com", Dictionary<string, string>? positions = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_password",
            FirstName = "Test",
            LastName = "User",
            Positions = positions ?? new Dictionary<string, string> { { "skater", "Silver" } },
            Role = "Player",
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Organization> CreateTestOrganization(Guid creatorId, string name = "Test Org")
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatorId = creatorId,
            IsActive = true
        };
        _context.Organizations.Add(org);

        _context.OrganizationAdmins.Add(new OrganizationAdmin
        {
            OrganizationId = org.Id,
            UserId = creatorId
        });

        await _context.SaveChangesAsync();
        return org;
    }

    private async Task Subscribe(Guid orgId, Guid userId)
    {
        _context.OrganizationSubscriptions.Add(new OrganizationSubscription
        {
            OrganizationId = orgId,
            UserId = userId
        });
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetAutoRosterAsync_AsNonAdmin_ThrowsUnauthorized()
    {
        var admin = await CreateTestUser("admin@example.com");
        var nonAdmin = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);

        var act = () => _sut.GetAutoRosterAsync(org.Id, nonAdmin.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AddMemberAsync_AsNonAdmin_ThrowsUnauthorized()
    {
        var admin = await CreateTestUser("admin@example.com");
        var nonAdmin = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await Subscribe(org.Id, nonAdmin.Id);

        var act = () => _sut.AddMemberAsync(org.Id, nonAdmin.Id, "Skater", nonAdmin.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RemoveMemberAsync_AsNonAdmin_ThrowsUnauthorized()
    {
        var admin = await CreateTestUser("admin@example.com");
        var nonAdmin = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);

        var act = () => _sut.RemoveMemberAsync(org.Id, nonAdmin.Id, nonAdmin.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ReorderAsync_AsNonAdmin_ThrowsUnauthorized()
    {
        var admin = await CreateTestUser("admin@example.com");
        var nonAdmin = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);

        var act = () => _sut.ReorderAsync(org.Id, new List<Guid>(), nonAdmin.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region Add Tests

    [Fact]
    public async Task AddMemberAsync_WithSubscriber_AddsAtEndOfOrder()
    {
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        await Subscribe(org.Id, user1.Id);
        await Subscribe(org.Id, user2.Id);

        var first = await _sut.AddMemberAsync(org.Id, user1.Id, "Skater", admin.Id);
        var second = await _sut.AddMemberAsync(org.Id, user2.Id, "Goalie", admin.Id);

        first.SortOrder.Should().Be(0);
        second.SortOrder.Should().Be(1);
        second.Position.Should().Be("Goalie");
        second.UserId.Should().Be(user2.Id);

        var saved = await _context.OrganizationAutoRosterMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == org.Id && m.UserId == user2.Id);
        saved.Should().NotBeNull();
        saved!.AddedByUserId.Should().Be(admin.Id);
    }

    [Fact]
    public async Task AddMemberAsync_WithNonSubscriber_ThrowsInvalidOperation()
    {
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var nonSubscriber = await CreateTestUser("outsider@example.com");

        var act = () => _sut.AddMemberAsync(org.Id, nonSubscriber.Id, "Skater", admin.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*member of this organization*");
    }

    [Fact]
    public async Task AddMemberAsync_WithDuplicateUser_ThrowsInvalidOperation()
    {
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var user = await CreateTestUser("user@example.com");
        await Subscribe(org.Id, user.Id);
        await _sut.AddMemberAsync(org.Id, user.Id, "Skater", admin.Id);

        var act = () => _sut.AddMemberAsync(org.Id, user.Id, "Goalie", admin.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in the auto-roster*");
    }

    [Fact]
    public async Task AddMemberAsync_WithInvalidPosition_ThrowsInvalidOperation()
    {
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var user = await CreateTestUser("user@example.com");
        await Subscribe(org.Id, user.Id);

        var act = () => _sut.AddMemberAsync(org.Id, user.Id, "Defense", admin.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid position*");
    }

    [Fact]
    public async Task AddMemberAsync_NormalizesPositionCasing()
    {
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var user = await CreateTestUser("user@example.com");
        await Subscribe(org.Id, user.Id);

        var result = await _sut.AddMemberAsync(org.Id, user.Id, "goalie", admin.Id);

        result.Position.Should().Be("Goalie");
    }

    [Fact]
    public async Task AddMemberAsync_DoesNotValidateAgainstProfilePositions()
    {
        // Organizer semantics: admin can list a skater-only user as a goalie
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var user = await CreateTestUser("user@example.com", new Dictionary<string, string> { { "skater", "Silver" } });
        await Subscribe(org.Id, user.Id);

        var result = await _sut.AddMemberAsync(org.Id, user.Id, "Goalie", admin.Id);

        result.Position.Should().Be("Goalie");
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task GetAutoRosterAsync_ReturnsMembersOrderedBySortOrder()
    {
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        await Subscribe(org.Id, user1.Id);
        await Subscribe(org.Id, user2.Id);
        await _sut.AddMemberAsync(org.Id, user1.Id, "Skater", admin.Id);
        await _sut.AddMemberAsync(org.Id, user2.Id, "Goalie", admin.Id);
        await _sut.ReorderAsync(org.Id, new List<Guid> { user2.Id, user1.Id }, admin.Id);

        var result = await _sut.GetAutoRosterAsync(org.Id, admin.Id);

        result.Should().HaveCount(2);
        result[0].UserId.Should().Be(user2.Id);
        result[1].UserId.Should().Be(user1.Id);
        result[0].FirstName.Should().Be("Test");
        result[0].Positions.Should().ContainKey("skater");
    }

    [Fact]
    public async Task GetAutoRosterAsync_WithEmptyList_ReturnsEmpty()
    {
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);

        var result = await _sut.GetAutoRosterAsync(org.Id, admin.Id);

        result.Should().BeEmpty();
    }

    #endregion

    #region Remove Tests

    [Fact]
    public async Task RemoveMemberAsync_WithExistingMember_RemovesAndReturnsTrue()
    {
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var user = await CreateTestUser("user@example.com");
        await Subscribe(org.Id, user.Id);
        await _sut.AddMemberAsync(org.Id, user.Id, "Skater", admin.Id);

        var result = await _sut.RemoveMemberAsync(org.Id, user.Id, admin.Id);

        result.Should().BeTrue();
        var remaining = await _context.OrganizationAutoRosterMembers
            .Where(m => m.OrganizationId == org.Id)
            .ToListAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveMemberAsync_WithNonMember_ReturnsFalse()
    {
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var user = await CreateTestUser("user@example.com");

        var result = await _sut.RemoveMemberAsync(org.Id, user.Id, admin.Id);

        result.Should().BeFalse();
    }

    #endregion

    #region Reorder Tests

    [Fact]
    public async Task ReorderAsync_WithAllMembers_PersistsNewOrder()
    {
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");
        await Subscribe(org.Id, user1.Id);
        await Subscribe(org.Id, user2.Id);
        await Subscribe(org.Id, user3.Id);
        await _sut.AddMemberAsync(org.Id, user1.Id, "Skater", admin.Id);
        await _sut.AddMemberAsync(org.Id, user2.Id, "Skater", admin.Id);
        await _sut.AddMemberAsync(org.Id, user3.Id, "Skater", admin.Id);

        var result = await _sut.ReorderAsync(org.Id, new List<Guid> { user3.Id, user1.Id, user2.Id }, admin.Id);

        result.Select(m => m.UserId).Should().ContainInOrder(user3.Id, user1.Id, user2.Id);

        var saved = await _context.OrganizationAutoRosterMembers
            .Where(m => m.OrganizationId == org.Id)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();
        saved.Select(m => m.UserId).Should().ContainInOrder(user3.Id, user1.Id, user2.Id);
    }

    [Fact]
    public async Task ReorderAsync_WithMissingMember_ThrowsInvalidOperation()
    {
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        await Subscribe(org.Id, user1.Id);
        await Subscribe(org.Id, user2.Id);
        await _sut.AddMemberAsync(org.Id, user1.Id, "Skater", admin.Id);
        await _sut.AddMemberAsync(org.Id, user2.Id, "Skater", admin.Id);

        var act = () => _sut.ReorderAsync(org.Id, new List<Guid> { user1.Id }, admin.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*All auto-roster members*");
    }

    [Fact]
    public async Task ReorderAsync_WithUnknownUser_ThrowsInvalidOperation()
    {
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var user1 = await CreateTestUser("user1@example.com");
        await Subscribe(org.Id, user1.Id);
        await _sut.AddMemberAsync(org.Id, user1.Id, "Skater", admin.Id);

        var act = () => _sut.ReorderAsync(org.Id, new List<Guid> { user1.Id, Guid.NewGuid() }, admin.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*All auto-roster members*");
    }

    #endregion
}
