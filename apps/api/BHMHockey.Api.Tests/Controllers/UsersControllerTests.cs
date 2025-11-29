using BHMHockey.Api.Controllers;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
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
    private readonly Mock<IOrganizationService> _mockOrgService;
    private readonly Mock<IEventService> _mockEventService;
    private readonly UsersController _sut;

    public UsersControllerTests()
    {
        _mockUserService = new Mock<IUserService>();
        _mockOrgService = new Mock<IOrganizationService>();
        _mockEventService = new Mock<IEventService>();
        _sut = new UsersController(_mockUserService.Object, _mockOrgService.Object, _mockEventService.Object);
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

    #region GetMySubscriptions Endpoint Tests

    [Fact]
    public async Task GetMySubscriptions_WithValidUser_ReturnsOkWithSubscriptions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgDto = new OrganizationDto(
            Guid.NewGuid(), "Test Org", "Description", "Boston", "Gold", Guid.NewGuid(), 10, true, false, DateTime.UtcNow);
        var subscriptions = new List<OrganizationSubscriptionDto>
        {
            new OrganizationSubscriptionDto(Guid.NewGuid(), orgDto, true, DateTime.UtcNow)
        };

        var claims = new List<Claim> { new Claim("sub", userId.ToString()) };
        SetupControllerWithClaims(claims);
        _mockOrgService.Setup(s => s.GetUserSubscriptionsAsync(userId)).ReturnsAsync(subscriptions);

        // Act
        var result = await _sut.GetMySubscriptions();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSubs = okResult.Value as List<OrganizationSubscriptionDto>;
        returnedSubs.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMySubscriptions_WithInvalidClaims_ReturnsUnauthorized()
    {
        // Arrange - no claims
        SetupControllerWithClaims(new List<Claim>());

        // Act
        var result = await _sut.GetMySubscriptions();

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetMySubscriptions_WithNoSubscriptions_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new List<Claim> { new Claim("sub", userId.ToString()) };
        SetupControllerWithClaims(claims);
        _mockOrgService.Setup(s => s.GetUserSubscriptionsAsync(userId))
            .ReturnsAsync(new List<OrganizationSubscriptionDto>());

        // Act
        var result = await _sut.GetMySubscriptions();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSubs = okResult.Value as List<OrganizationSubscriptionDto>;
        returnedSubs.Should().BeEmpty();
    }

    #endregion

    #region GetMyRegistrations Endpoint Tests

    [Fact]
    public async Task GetMyRegistrations_WithValidUser_ReturnsOkWithEvents()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var events = new List<EventDto>
        {
            new EventDto(
                Id: Guid.NewGuid(),
                OrganizationId: Guid.NewGuid(),
                OrganizationName: "Test Org",
                CreatorId: userId,
                Name: "Test Event",
                Description: "Description",
                EventDate: DateTime.UtcNow.AddDays(7),
                Duration: 60,
                Venue: "Test Venue",
                MaxPlayers: 20,
                RegisteredCount: 5,
                Cost: 25.00m,
                RegistrationDeadline: DateTime.UtcNow.AddDays(6),
                Status: "Published",
                Visibility: "Public",
                IsRegistered: true,
                IsCreator: false,
                CreatedAt: DateTime.UtcNow,
                CreatorVenmoHandle: null,    // Phase 4
                MyPaymentStatus: null,       // Phase 4
                UnpaidCount: null            // Organizer view
            )
        };

        var claims = new List<Claim> { new Claim("sub", userId.ToString()) };
        SetupControllerWithClaims(claims);
        _mockEventService.Setup(s => s.GetUserRegistrationsAsync(userId)).ReturnsAsync(events);

        // Act
        var result = await _sut.GetMyRegistrations();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedEvents = okResult.Value as List<EventDto>;
        returnedEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMyRegistrations_WithInvalidClaims_ReturnsUnauthorized()
    {
        // Arrange - no claims
        SetupControllerWithClaims(new List<Claim>());

        // Act
        var result = await _sut.GetMyRegistrations();

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetMyRegistrations_WithNoRegistrations_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new List<Claim> { new Claim("sub", userId.ToString()) };
        SetupControllerWithClaims(claims);
        _mockEventService.Setup(s => s.GetUserRegistrationsAsync(userId))
            .ReturnsAsync(new List<EventDto>());

        // Act
        var result = await _sut.GetMyRegistrations();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedEvents = okResult.Value as List<EventDto>;
        returnedEvents.Should().BeEmpty();
    }

    #endregion
}
