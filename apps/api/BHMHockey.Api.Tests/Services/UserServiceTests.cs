using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for UserService - protecting user profile integrity.
/// These tests ensure profile updates don't corrupt data.
/// </summary>
public class UserServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _sut = new UserService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task<User> CreateTestUser(
        string email = "test@example.com",
        string firstName = "John",
        string lastName = "Doe",
        string? skillLevel = null,
        string? position = null,
        string? venmoHandle = null,
        string? phoneNumber = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_password",
            FirstName = firstName,
            LastName = lastName,
            SkillLevel = skillLevel,
            Position = position,
            VenmoHandle = venmoHandle,
            PhoneNumber = phoneNumber,
            Role = "Player",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    #region GetUserById Tests

    [Fact]
    public async Task GetUserByIdAsync_WithExistingUser_ReturnsUserDto()
    {
        // Arrange
        var user = await CreateTestUser();

        // Act
        var result = await _sut.GetUserByIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(user.Email);
        result.FirstName.Should().Be(user.FirstName);
        result.LastName.Should().Be(user.LastName);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _sut.GetUserByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UpdateProfile Tests

    [Fact]
    public async Task UpdateProfileAsync_WithPartialData_OnlyUpdatesProvidedFields()
    {
        // Arrange
        var user = await CreateTestUser(
            firstName: "John",
            lastName: "Doe",
            skillLevel: "Gold",
            position: "Forward"
        );

        var request = new UpdateUserProfileRequest(
            FirstName: "Jane",
            LastName: null,
            PhoneNumber: null,
            SkillLevel: null,
            Position: null,
            VenmoHandle: null
        );

        // Act
        var result = await _sut.UpdateProfileAsync(user.Id, request);

        // Assert
        result.FirstName.Should().Be("Jane");      // Updated
        result.LastName.Should().Be("Doe");        // Preserved
        result.SkillLevel.Should().Be("Gold");     // Preserved
        result.Position.Should().Be("Forward");    // Preserved
    }

    [Fact]
    public async Task UpdateProfileAsync_WithNullValues_PreservesExistingData()
    {
        // Arrange
        var user = await CreateTestUser(
            firstName: "John",
            lastName: "Doe",
            skillLevel: "Silver",
            position: "Defense",
            venmoHandle: "john-doe",
            phoneNumber: "1234567890"
        );

        var request = new UpdateUserProfileRequest(
            FirstName: null,
            LastName: null,
            PhoneNumber: null,
            SkillLevel: null,
            Position: null,
            VenmoHandle: null
        );

        // Act
        var result = await _sut.UpdateProfileAsync(user.Id, request);

        // Assert
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.SkillLevel.Should().Be("Silver");
        result.Position.Should().Be("Defense");
        result.VenmoHandle.Should().Be("john-doe");
        result.PhoneNumber.Should().Be("1234567890");
    }

    [Fact]
    public async Task UpdateProfileAsync_SetsUpdatedAtToUtcNow()
    {
        // Arrange
        var user = await CreateTestUser();
        var originalUpdatedAt = user.UpdatedAt;

        // Wait a bit to ensure time difference
        await Task.Delay(10);

        var request = new UpdateUserProfileRequest(
            FirstName: "Updated",
            LastName: null,
            PhoneNumber: null,
            SkillLevel: null,
            Position: null,
            VenmoHandle: null
        );

        // Act
        await _sut.UpdateProfileAsync(user.Id, request);
        var updatedUser = await _context.Users.FindAsync(user.Id);

        // Assert
        updatedUser!.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        updatedUser.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpdateProfileAsync_WithNonExistentUser_ThrowsException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = new UpdateUserProfileRequest(
            FirstName: "Test",
            LastName: null,
            PhoneNumber: null,
            SkillLevel: null,
            Position: null,
            VenmoHandle: null
        );

        // Act
        var act = () => _sut.UpdateProfileAsync(nonExistentId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateProfileAsync_WithAllFields_UpdatesAllFields()
    {
        // Arrange
        var user = await CreateTestUser();
        var request = new UpdateUserProfileRequest(
            FirstName: "Jane",
            LastName: "Smith",
            PhoneNumber: "9876543210",
            SkillLevel: "Gold",
            Position: "Goalie",
            VenmoHandle: "jane-smith"
        );

        // Act
        var result = await _sut.UpdateProfileAsync(user.Id, request);

        // Assert
        result.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Smith");
        result.PhoneNumber.Should().Be("9876543210");
        result.SkillLevel.Should().Be("Gold");
        result.Position.Should().Be("Goalie");
        result.VenmoHandle.Should().Be("jane-smith");
    }

    #endregion

    #region UpdatePushToken Tests

    [Fact]
    public async Task UpdatePushTokenAsync_WithValidUser_UpdatesToken()
    {
        // Arrange
        var user = await CreateTestUser();
        var pushToken = "ExponentPushToken[xxxxxxxxxxxxxxxxxxxxxx]";

        // Act
        await _sut.UpdatePushTokenAsync(user.Id, pushToken);
        var updatedUser = await _context.Users.FindAsync(user.Id);

        // Assert
        updatedUser!.PushToken.Should().Be(pushToken);
    }

    [Fact]
    public async Task UpdatePushTokenAsync_WithNonExistentUser_ThrowsException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _sut.UpdatePushTokenAsync(nonExistentId, "some-token");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdatePushTokenAsync_SetsUpdatedAt()
    {
        // Arrange
        var user = await CreateTestUser();
        var originalUpdatedAt = user.UpdatedAt;
        await Task.Delay(10);

        // Act
        await _sut.UpdatePushTokenAsync(user.Id, "new-token");
        var updatedUser = await _context.Users.FindAsync(user.Id);

        // Assert
        updatedUser!.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    #endregion
}
