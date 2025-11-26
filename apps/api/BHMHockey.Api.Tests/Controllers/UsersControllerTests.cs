using BHMHockey.Api.Controllers;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace BHMHockey.Api.Tests.Controllers;

/// <summary>
/// Tests for UsersController - verifying correct extraction of user identity from JWT claims.
/// These tests protect against authentication bypass and incorrect user identification.
/// </summary>
public class UsersControllerTests
{
    private readonly Mock<IUserService> _mockUserService;
    private readonly UsersController _sut;

    public UsersControllerTests()
    {
        _mockUserService = new Mock<IUserService>();
        _sut = new UsersController(_mockUserService.Object);
    }

    private void SetupControllerWithClaims(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private Guid InvokeGetCurrentUserId()
    {
        // Use reflection to test the private method
        var method = typeof(UsersController).GetMethod(
            "GetCurrentUserId",
            BindingFlags.NonPublic | BindingFlags.Instance);

        return (Guid)method!.Invoke(_sut, null)!;
    }

    #region GetCurrentUserId Tests

    [Fact]
    public void GetCurrentUserId_WithValidSubClaim_ReturnsGuid()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var claims = new List<Claim> { new Claim("sub", expectedId.ToString()) };
        SetupControllerWithClaims(claims);

        // Act
        var result = InvokeGetCurrentUserId();

        // Assert
        result.Should().Be(expectedId);
    }

    [Fact]
    public void GetCurrentUserId_WithNameIdentifierClaim_ReturnsGuid()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, expectedId.ToString()) };
        SetupControllerWithClaims(claims);

        // Act
        var result = InvokeGetCurrentUserId();

        // Assert
        result.Should().Be(expectedId);
    }

    [Fact]
    public void GetCurrentUserId_WithNoClaims_ThrowsUnauthorized()
    {
        // Arrange
        SetupControllerWithClaims(new List<Claim>());

        // Act
        var act = () => InvokeGetCurrentUserId();

        // Assert
        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<UnauthorizedAccessException>();
    }

    [Fact]
    public void GetCurrentUserId_WithInvalidGuid_ThrowsUnauthorized()
    {
        // Arrange
        var claims = new List<Claim> { new Claim("sub", "not-a-guid") };
        SetupControllerWithClaims(claims);

        // Act
        var act = () => InvokeGetCurrentUserId();

        // Assert
        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<UnauthorizedAccessException>();
    }

    #endregion

    #region GetCurrentUser Endpoint Tests

    [Fact]
    public async Task GetCurrentUser_WithValidUser_ReturnsOkWithUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedUser = new UserDto(
            userId, "test@example.com", "John", "Doe", null, null, null, null, "Player", DateTime.UtcNow);

        var claims = new List<Claim> { new Claim("sub", userId.ToString()) };
        SetupControllerWithClaims(claims);
        _mockUserService.Setup(s => s.GetUserByIdAsync(userId)).ReturnsAsync(expectedUser);

        // Act
        var result = await _sut.GetCurrentUser();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedUser);
    }

    [Fact]
    public async Task GetCurrentUser_WithNonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new List<Claim> { new Claim("sub", userId.ToString()) };
        SetupControllerWithClaims(claims);
        _mockUserService.Setup(s => s.GetUserByIdAsync(userId)).ReturnsAsync((UserDto?)null);

        // Act
        var result = await _sut.GetCurrentUser();

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCurrentUser_WithInvalidClaims_ReturnsUnauthorized()
    {
        // Arrange - no claims
        SetupControllerWithClaims(new List<Claim>());

        // Act
        var result = await _sut.GetCurrentUser();

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region UpdateProfile Endpoint Tests

    [Fact]
    public async Task UpdateProfile_WithValidRequest_ReturnsOkWithUpdatedUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateUserProfileRequest("Jane", "Smith", "1234567890", "Gold", "Forward", "jane-smith");
        var expectedUser = new UserDto(
            userId, "test@example.com", "Jane", "Smith", "1234567890", "Gold", "Forward", "jane-smith", "Player", DateTime.UtcNow);

        var claims = new List<Claim> { new Claim("sub", userId.ToString()) };
        SetupControllerWithClaims(claims);
        _mockUserService.Setup(s => s.UpdateProfileAsync(userId, request)).ReturnsAsync(expectedUser);

        // Act
        var result = await _sut.UpdateProfile(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedUser);
    }

    [Fact]
    public async Task UpdateProfile_WithInvalidUser_ReturnsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateUserProfileRequest("Jane", null, null, null, null, null);

        var claims = new List<Claim> { new Claim("sub", userId.ToString()) };
        SetupControllerWithClaims(claims);
        _mockUserService.Setup(s => s.UpdateProfileAsync(userId, request))
            .ThrowsAsync(new InvalidOperationException("User not found"));

        // Act
        var result = await _sut.UpdateProfile(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region UpdatePushToken Endpoint Tests

    [Fact]
    public async Task UpdatePushToken_WithValidToken_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdatePushTokenRequest("ExponentPushToken[xxxxx]");

        var claims = new List<Claim> { new Claim("sub", userId.ToString()) };
        SetupControllerWithClaims(claims);
        _mockUserService.Setup(s => s.UpdatePushTokenAsync(userId, request.PushToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.UpdatePushToken(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdatePushToken_WithInvalidUser_ReturnsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdatePushTokenRequest("token");

        var claims = new List<Claim> { new Claim("sub", userId.ToString()) };
        SetupControllerWithClaims(claims);
        _mockUserService.Setup(s => s.UpdatePushTokenAsync(userId, request.PushToken))
            .ThrowsAsync(new InvalidOperationException("User not found"));

        // Act
        var result = await _sut.UpdatePushToken(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
