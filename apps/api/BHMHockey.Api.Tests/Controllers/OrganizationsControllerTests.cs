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
/// Tests for OrganizationsController - verifying the API contract for organization operations.
/// These tests verify authorization, correct status codes, and proper delegation to services.
/// </summary>
public class OrganizationsControllerTests
{
    private readonly Mock<IOrganizationService> _mockOrgService;
    private readonly OrganizationsController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public OrganizationsControllerTests()
    {
        _mockOrgService = new Mock<IOrganizationService>();
        _controller = new OrganizationsController(_mockOrgService.Object);
    }

    #region GetAll Tests

    [Fact]
    public async Task GetAll_WithNoAuth_ReturnsOrganizationsWithoutSubscriptionStatus()
    {
        // Arrange
        var orgs = new List<OrganizationDto>
        {
            CreateOrgDto("Org 1", isSubscribed: false),
            CreateOrgDto("Org 2", isSubscribed: false)
        };
        _mockOrgService.Setup(s => s.GetAllAsync(null)).ReturnsAsync(orgs);

        // Act
        var result = await _controller.GetAll();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedOrgs = okResult!.Value as List<OrganizationDto>;
        returnedOrgs.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_WithAuth_ReturnsOrganizationsWithSubscriptionStatus()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgs = new List<OrganizationDto>
        {
            CreateOrgDto("Org 1", isSubscribed: true),
            CreateOrgDto("Org 2", isSubscribed: false)
        };
        _mockOrgService.Setup(s => s.GetAllAsync(_testUserId)).ReturnsAsync(orgs);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = result.Result as OkObjectResult;
        var returnedOrgs = okResult!.Value as List<OrganizationDto>;
        returnedOrgs!.First().IsSubscribed.Should().BeTrue();
        returnedOrgs!.Last().IsSubscribed.Should().BeFalse();
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetById_WithValidId_ReturnsOrganization()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var org = CreateOrgDto("Test Org");
        _mockOrgService.Setup(s => s.GetByIdAsync(orgId, null)).ReturnsAsync(org);

        // Act
        var result = await _controller.GetById(orgId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WithInvalidId_Returns404()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        _mockOrgService.Setup(s => s.GetByIdAsync(orgId, null)).ReturnsAsync((OrganizationDto?)null);

        // Act
        var result = await _controller.GetById(orgId);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedOrganization()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var request = new CreateOrganizationRequest("New Org", "Description", "Boston", "Gold");
        var createdOrg = CreateOrgDto("New Org");
        _mockOrgService.Setup(s => s.CreateAsync(request, _testUserId)).ReturnsAsync(createdOrg);

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult!.Value.Should().BeEquivalentTo(createdOrg);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_AsCreator_ReturnsUpdatedOrganization()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var request = new UpdateOrganizationRequest("Updated Name", null, null, null);
        var updatedOrg = CreateOrgDto("Updated Name");
        _mockOrgService.Setup(s => s.UpdateAsync(orgId, request, _testUserId)).ReturnsAsync(updatedOrg);

        // Act
        var result = await _controller.Update(orgId, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Update_AsNonCreator_Returns404()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var request = new UpdateOrganizationRequest("Updated Name", null, null, null);
        _mockOrgService.Setup(s => s.UpdateAsync(orgId, request, _testUserId)).ReturnsAsync((OrganizationDto?)null);

        // Act
        var result = await _controller.Update(orgId, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_AsCreator_Returns204NoContent()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        _mockOrgService.Setup(s => s.DeleteAsync(orgId, _testUserId)).ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(orgId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_AsNonCreator_Returns404()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        _mockOrgService.Setup(s => s.DeleteAsync(orgId, _testUserId)).ReturnsAsync(false);

        // Act
        var result = await _controller.Delete(orgId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Subscribe Tests

    [Fact]
    public async Task Subscribe_WhenNotSubscribed_ReturnsSuccess()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        _mockOrgService.Setup(s => s.SubscribeAsync(orgId, _testUserId)).ReturnsAsync(true);

        // Act
        var result = await _controller.Subscribe(orgId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Subscribe_WhenAlreadySubscribed_Returns400()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        _mockOrgService.Setup(s => s.SubscribeAsync(orgId, _testUserId)).ReturnsAsync(false);

        // Act
        var result = await _controller.Subscribe(orgId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Unsubscribe Tests

    [Fact]
    public async Task Unsubscribe_WhenSubscribed_Returns204()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        _mockOrgService.Setup(s => s.UnsubscribeAsync(orgId, _testUserId)).ReturnsAsync(true);

        // Act
        var result = await _controller.Unsubscribe(orgId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Unsubscribe_WhenNotSubscribed_Returns404()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        _mockOrgService.Setup(s => s.UnsubscribeAsync(orgId, _testUserId)).ReturnsAsync(false);

        // Act
        var result = await _controller.Unsubscribe(orgId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region Helper Methods

    private void SetupAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private OrganizationDto CreateOrgDto(string name, bool isSubscribed = false)
    {
        return new OrganizationDto(
            Guid.NewGuid(),
            name,
            "Description",
            "Location",
            "Gold",
            Guid.NewGuid(),
            10,
            isSubscribed,
            DateTime.UtcNow
        );
    }

    #endregion
}
