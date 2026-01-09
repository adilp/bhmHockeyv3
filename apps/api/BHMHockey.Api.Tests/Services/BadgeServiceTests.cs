using BHMHockey.Api.Data;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for BadgeService - ensuring badge ordering and management work correctly.
/// </summary>
public class BadgeServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly BadgeService _sut;

    public BadgeServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _sut = new BadgeService(_context);
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

    private async Task<BadgeType> CreateBadgeType(string code, string name, int sortPriority)
    {
        var badgeType = new BadgeType
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Description = $"Test badge: {name}",
            IconName = $"icon_{code}",
            Category = "achievement",
            SortPriority = sortPriority,
            CreatedAt = DateTime.UtcNow
        };

        _context.BadgeTypes.Add(badgeType);
        await _context.SaveChangesAsync();
        return badgeType;
    }

    private async Task<UserBadge> CreateUserBadge(Guid userId, Guid badgeTypeId, int? displayOrder = null)
    {
        var userBadge = new UserBadge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BadgeTypeId = badgeTypeId,
            Context = new Dictionary<string, object> { { "test", true } },
            EarnedAt = DateTime.UtcNow,
            DisplayOrder = displayOrder
        };

        _context.UserBadges.Add(userBadge);
        await _context.SaveChangesAsync();
        return userBadge;
    }

    #endregion

    #region Ordering Tests

    [Fact]
    public async Task GetUserBadgesAsync_OrdersByDisplayOrderFirst_ThenBySortPriority()
    {
        // Arrange
        var user = await CreateTestUser();

        // Create badge types with different sort priorities
        var goldBadge = await CreateBadgeType("gold", "Gold Badge", sortPriority: 1);
        var silverBadge = await CreateBadgeType("silver", "Silver Badge", sortPriority: 2);
        var bronzeBadge = await CreateBadgeType("bronze", "Bronze Badge", sortPriority: 3);

        // Assign badges with NO custom displayOrder (should use sortPriority)
        await CreateUserBadge(user.Id, bronzeBadge.Id, displayOrder: null);  // sortPriority: 3
        await CreateUserBadge(user.Id, goldBadge.Id, displayOrder: null);    // sortPriority: 1
        await CreateUserBadge(user.Id, silverBadge.Id, displayOrder: null);  // sortPriority: 2

        // Act
        var result = await _sut.GetUserBadgesAsync(user.Id);

        // Assert - should be ordered by sortPriority when displayOrder is null
        result.Should().HaveCount(3);
        result[0].BadgeType.Code.Should().Be("gold");    // sortPriority: 1
        result[1].BadgeType.Code.Should().Be("silver");  // sortPriority: 2
        result[2].BadgeType.Code.Should().Be("bronze");  // sortPriority: 3
    }

    [Fact]
    public async Task GetUserBadgesAsync_DisplayOrderOverridesSortPriority()
    {
        // Arrange
        var user = await CreateTestUser();

        var goldBadge = await CreateBadgeType("gold", "Gold Badge", sortPriority: 1);
        var silverBadge = await CreateBadgeType("silver", "Silver Badge", sortPriority: 2);
        var bronzeBadge = await CreateBadgeType("bronze", "Bronze Badge", sortPriority: 3);

        // User customizes display order: bronze first, then gold, then silver
        await CreateUserBadge(user.Id, bronzeBadge.Id, displayOrder: 0);  // User wants this first
        await CreateUserBadge(user.Id, goldBadge.Id, displayOrder: 1);    // User wants this second
        await CreateUserBadge(user.Id, silverBadge.Id, displayOrder: 2);  // User wants this third

        // Act
        var result = await _sut.GetUserBadgesAsync(user.Id);

        // Assert - should respect custom displayOrder over sortPriority
        result.Should().HaveCount(3);
        result[0].BadgeType.Code.Should().Be("bronze");  // displayOrder: 0
        result[1].BadgeType.Code.Should().Be("gold");    // displayOrder: 1
        result[2].BadgeType.Code.Should().Be("silver");  // displayOrder: 2
    }

    [Fact]
    public async Task GetUserBadgesAsync_MixedDisplayOrder_NullsSortByPriorityAfterCustomOrder()
    {
        // Arrange
        var user = await CreateTestUser();

        var goldBadge = await CreateBadgeType("gold", "Gold Badge", sortPriority: 1);
        var silverBadge = await CreateBadgeType("silver", "Silver Badge", sortPriority: 2);
        var bronzeBadge = await CreateBadgeType("bronze", "Bronze Badge", sortPriority: 3);
        var platinumBadge = await CreateBadgeType("platinum", "Platinum Badge", sortPriority: 0);

        // Mix of custom displayOrder and nulls
        await CreateUserBadge(user.Id, bronzeBadge.Id, displayOrder: 0);   // Custom: first
        await CreateUserBadge(user.Id, goldBadge.Id, displayOrder: null);  // Null: uses sortPriority 1
        await CreateUserBadge(user.Id, silverBadge.Id, displayOrder: null); // Null: uses sortPriority 2
        await CreateUserBadge(user.Id, platinumBadge.Id, displayOrder: null); // Null: uses sortPriority 0

        // Act
        var result = await _sut.GetUserBadgesAsync(user.Id);

        // Assert
        // First: bronze (displayOrder: 0)
        // Then nulls sorted by sortPriority: platinum (0), gold (1), silver (2)
        result.Should().HaveCount(4);
        result[0].BadgeType.Code.Should().Be("bronze");   // displayOrder: 0
        result[1].BadgeType.Code.Should().Be("platinum"); // null -> sortPriority: 0
        result[2].BadgeType.Code.Should().Be("gold");     // null -> sortPriority: 1
        result[3].BadgeType.Code.Should().Be("silver");   // null -> sortPriority: 2
    }

    [Fact]
    public async Task GetUserTopBadgesAsync_ReturnsOnlyTopN_InCorrectOrder()
    {
        // Arrange
        var user = await CreateTestUser();

        var badge1 = await CreateBadgeType("badge1", "Badge 1", sortPriority: 1);
        var badge2 = await CreateBadgeType("badge2", "Badge 2", sortPriority: 2);
        var badge3 = await CreateBadgeType("badge3", "Badge 3", sortPriority: 3);
        var badge4 = await CreateBadgeType("badge4", "Badge 4", sortPriority: 4);
        var badge5 = await CreateBadgeType("badge5", "Badge 5", sortPriority: 5);

        // All with null displayOrder - should use sortPriority
        await CreateUserBadge(user.Id, badge5.Id, displayOrder: null);
        await CreateUserBadge(user.Id, badge3.Id, displayOrder: null);
        await CreateUserBadge(user.Id, badge1.Id, displayOrder: null);
        await CreateUserBadge(user.Id, badge4.Id, displayOrder: null);
        await CreateUserBadge(user.Id, badge2.Id, displayOrder: null);

        // Act
        var result = await _sut.GetUserTopBadgesAsync(user.Id, count: 3);

        // Assert - should return top 3 by sortPriority
        result.Should().HaveCount(3);
        result[0].BadgeType.Code.Should().Be("badge1");  // sortPriority: 1
        result[1].BadgeType.Code.Should().Be("badge2");  // sortPriority: 2
        result[2].BadgeType.Code.Should().Be("badge3");  // sortPriority: 3
    }

    [Fact]
    public async Task GetUserTopBadgesAsync_WithCustomDisplayOrder_RespectsUserPreference()
    {
        // Arrange
        var user = await CreateTestUser();

        var badge1 = await CreateBadgeType("badge1", "Badge 1", sortPriority: 1);
        var badge2 = await CreateBadgeType("badge2", "Badge 2", sortPriority: 2);
        var badge3 = await CreateBadgeType("badge3", "Badge 3", sortPriority: 3);
        var badge4 = await CreateBadgeType("badge4", "Badge 4", sortPriority: 4);

        // User customizes: badge4 first, badge3 second, badge2 third, badge1 last
        await CreateUserBadge(user.Id, badge4.Id, displayOrder: 0);
        await CreateUserBadge(user.Id, badge3.Id, displayOrder: 1);
        await CreateUserBadge(user.Id, badge2.Id, displayOrder: 2);
        await CreateUserBadge(user.Id, badge1.Id, displayOrder: 3);

        // Act
        var result = await _sut.GetUserTopBadgesAsync(user.Id, count: 3);

        // Assert - should return top 3 by displayOrder (user's preference)
        result.Should().HaveCount(3);
        result[0].BadgeType.Code.Should().Be("badge4");  // displayOrder: 0
        result[1].BadgeType.Code.Should().Be("badge3");  // displayOrder: 1
        result[2].BadgeType.Code.Should().Be("badge2");  // displayOrder: 2
    }

    #endregion

    #region UpdateBadgeOrder Tests

    [Fact]
    public async Task UpdateBadgeOrderAsync_UpdatesDisplayOrder_BasedOnArrayIndex()
    {
        // Arrange
        var user = await CreateTestUser();

        var badge1 = await CreateBadgeType("badge1", "Badge 1", sortPriority: 1);
        var badge2 = await CreateBadgeType("badge2", "Badge 2", sortPriority: 2);
        var badge3 = await CreateBadgeType("badge3", "Badge 3", sortPriority: 3);

        var ub1 = await CreateUserBadge(user.Id, badge1.Id);
        var ub2 = await CreateUserBadge(user.Id, badge2.Id);
        var ub3 = await CreateUserBadge(user.Id, badge3.Id);

        // Act - reorder: badge3 first, badge1 second, badge2 third
        await _sut.UpdateBadgeOrderAsync(user.Id, new List<Guid> { ub3.Id, ub1.Id, ub2.Id });

        // Assert
        var result = await _sut.GetUserBadgesAsync(user.Id);
        result[0].BadgeType.Code.Should().Be("badge3");  // displayOrder: 0
        result[1].BadgeType.Code.Should().Be("badge1");  // displayOrder: 1
        result[2].BadgeType.Code.Should().Be("badge2");  // displayOrder: 2
    }

    [Fact]
    public async Task UpdateBadgeOrderAsync_ThrowsOnDuplicateBadgeIds()
    {
        // Arrange
        var user = await CreateTestUser();
        var badge1 = await CreateBadgeType("badge1", "Badge 1", sortPriority: 1);
        var ub1 = await CreateUserBadge(user.Id, badge1.Id);

        // Act & Assert
        var act = () => _sut.UpdateBadgeOrderAsync(user.Id, new List<Guid> { ub1.Id, ub1.Id });
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Duplicate*");
    }

    [Fact]
    public async Task UpdateBadgeOrderAsync_ThrowsWhenNotAllBadgesIncluded()
    {
        // Arrange
        var user = await CreateTestUser();
        var badge1 = await CreateBadgeType("badge1", "Badge 1", sortPriority: 1);
        var badge2 = await CreateBadgeType("badge2", "Badge 2", sortPriority: 2);
        var ub1 = await CreateUserBadge(user.Id, badge1.Id);
        await CreateUserBadge(user.Id, badge2.Id);

        // Act & Assert - only including one badge when user has two
        var act = () => _sut.UpdateBadgeOrderAsync(user.Id, new List<Guid> { ub1.Id });
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*all badges*");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetUserBadgesAsync_ReturnsEmptyList_WhenUserHasNoBadges()
    {
        // Arrange
        var user = await CreateTestUser();

        // Act
        var result = await _sut.GetUserBadgesAsync(user.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserTopBadgesAsync_ReturnsAllBadges_WhenUserHasFewerThanRequested()
    {
        // Arrange
        var user = await CreateTestUser();
        var badge1 = await CreateBadgeType("badge1", "Badge 1", sortPriority: 1);
        var badge2 = await CreateBadgeType("badge2", "Badge 2", sortPriority: 2);
        await CreateUserBadge(user.Id, badge1.Id);
        await CreateUserBadge(user.Id, badge2.Id);

        // Act - request 3 but user only has 2
        var result = await _sut.GetUserTopBadgesAsync(user.Id, count: 3);

        // Assert
        result.Should().HaveCount(2);
        result[0].BadgeType.Code.Should().Be("badge1");
        result[1].BadgeType.Code.Should().Be("badge2");
    }

    #endregion
}
