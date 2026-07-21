using BHMHockey.Api.Controllers;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
    private readonly Mock<IOrganizationAdminService> _mockAdminService;
    private readonly Mock<IOrganizationAutoRosterService> _mockAutoRosterService;
    private readonly Mock<IOrganizationWaiverService> _mockWaiverService;
    private readonly OrganizationsController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public OrganizationsControllerTests()
    {
        _mockOrgService = new Mock<IOrganizationService>();
        _mockAdminService = new Mock<IOrganizationAdminService>();
        _mockAutoRosterService = new Mock<IOrganizationAutoRosterService>();
        _mockWaiverService = new Mock<IOrganizationWaiverService>();
        _controller = new OrganizationsController(
            _mockOrgService.Object,
            _mockAdminService.Object,
            _mockAutoRosterService.Object,
            _mockWaiverService.Object,
            Mock.Of<ILogger<OrganizationsController>>());
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
        var request = new CreateOrganizationRequest("New Org", "Description", "Boston", new List<string> { "Gold" }, null, null, null, null, null, null, null);
        var createdOrg = CreateOrgDto("New Org");
        _mockOrgService.Setup(s => s.CreateAsync(request, _testUserId)).ReturnsAsync(createdOrg);

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult!.Value.Should().BeEquivalentTo(createdOrg);
    }

    [Fact]
    public async Task Create_WhenServiceThrowsInvalidOperation_ReturnsBadRequest()
    {
        // Arrange - e.g. invalid GroupMe link or duplicate name
        SetupAuthenticatedUser(_testUserId);
        var request = new CreateOrganizationRequest("New Org", null, null, null, null, null, null, null, null, null, null, "https://discord.gg/abc");
        _mockOrgService.Setup(s => s.CreateAsync(request, _testUserId))
            .ThrowsAsync(new InvalidOperationException("GroupMe link must be an https://groupme.com URL (e.g., https://groupme.com/join_group/...)."));

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_AsCreator_ReturnsUpdatedOrganization()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var request = new UpdateOrganizationRequest("Updated Name", null, null, null, null, null, null, null, null, null, null);
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
        var request = new UpdateOrganizationRequest("Updated Name", null, null, null, null, null, null, null, null, null, null);
        _mockOrgService.Setup(s => s.UpdateAsync(orgId, request, _testUserId)).ReturnsAsync((OrganizationDto?)null);

        // Act
        var result = await _controller.Update(orgId, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Update_WhenServiceThrowsInvalidOperation_ReturnsBadRequest()
    {
        // Arrange - e.g. invalid GroupMe link
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var request = new UpdateOrganizationRequest(null, null, null, null, null, null, null, null, null, null, null, "http://groupme.com/join_group/abc");
        _mockOrgService.Setup(s => s.UpdateAsync(orgId, request, _testUserId))
            .ThrowsAsync(new InvalidOperationException("GroupMe link must be an https://groupme.com URL (e.g., https://groupme.com/join_group/...)."));

        // Act
        var result = await _controller.Update(orgId, request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
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

    #region Auto-Roster Tests

    [Fact]
    public async Task GetAutoRoster_AsAdmin_ReturnsMembers()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var members = new List<AutoRosterMemberDto> { CreateAutoRosterMemberDto(sortOrder: 0) };
        _mockAutoRosterService.Setup(s => s.GetAutoRosterAsync(orgId, _testUserId)).ReturnsAsync(members);

        // Act
        var result = await _controller.GetAutoRoster(orgId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        (okResult!.Value as List<AutoRosterMemberDto>).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAutoRoster_AsNonAdmin_ReturnsForbidden()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        _mockAutoRosterService.Setup(s => s.GetAutoRosterAsync(orgId, _testUserId))
            .ThrowsAsync(new UnauthorizedAccessException("Not an admin"));

        // Act
        var result = await _controller.GetAutoRoster(orgId);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task AddAutoRosterMember_WithValidRequest_ReturnsMember()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var request = new AddAutoRosterMemberRequest(memberUserId, "Skater");
        var member = CreateAutoRosterMemberDto(userId: memberUserId);
        _mockAutoRosterService.Setup(s => s.AddMemberAsync(orgId, memberUserId, "Skater", _testUserId))
            .ReturnsAsync(member);

        // Act
        var result = await _controller.AddAutoRosterMember(orgId, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        (okResult!.Value as AutoRosterMemberDto)!.UserId.Should().Be(memberUserId);
    }

    [Fact]
    public async Task AddAutoRosterMember_WhenNotSubscriber_Returns400()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var request = new AddAutoRosterMemberRequest(Guid.NewGuid(), "Skater");
        _mockAutoRosterService.Setup(s => s.AddMemberAsync(orgId, request.UserId, "Skater", _testUserId))
            .ThrowsAsync(new InvalidOperationException("User must be a member of this organization"));

        // Act
        var result = await _controller.AddAutoRosterMember(orgId, request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddAutoRosterMember_AsNonAdmin_ReturnsForbidden()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var request = new AddAutoRosterMemberRequest(Guid.NewGuid(), "Skater");
        _mockAutoRosterService.Setup(s => s.AddMemberAsync(orgId, request.UserId, "Skater", _testUserId))
            .ThrowsAsync(new UnauthorizedAccessException("Not an admin"));

        // Act
        var result = await _controller.AddAutoRosterMember(orgId, request);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task RemoveAutoRosterMember_WhenInList_Returns204()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        _mockAutoRosterService.Setup(s => s.RemoveMemberAsync(orgId, memberUserId, _testUserId)).ReturnsAsync(true);

        // Act
        var result = await _controller.RemoveAutoRosterMember(orgId, memberUserId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RemoveAutoRosterMember_WhenNotInList_Returns404()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        _mockAutoRosterService.Setup(s => s.RemoveMemberAsync(orgId, memberUserId, _testUserId)).ReturnsAsync(false);

        // Act
        var result = await _controller.RemoveAutoRosterMember(orgId, memberUserId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ReorderAutoRoster_WithValidOrder_ReturnsMembers()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var orderedIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var request = new ReorderAutoRosterRequest(orderedIds);
        var members = new List<AutoRosterMemberDto>
        {
            CreateAutoRosterMemberDto(userId: orderedIds[0], sortOrder: 0),
            CreateAutoRosterMemberDto(userId: orderedIds[1], sortOrder: 1)
        };
        _mockAutoRosterService.Setup(s => s.ReorderAsync(orgId, orderedIds, _testUserId)).ReturnsAsync(members);

        // Act
        var result = await _controller.ReorderAutoRoster(orgId, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ReorderAutoRoster_WithIncompleteList_Returns400()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var request = new ReorderAutoRosterRequest(new List<Guid> { Guid.NewGuid() });
        _mockAutoRosterService.Setup(s => s.ReorderAsync(orgId, request.OrderedUserIds, _testUserId))
            .ThrowsAsync(new InvalidOperationException("All auto-roster members must be included exactly once"));

        // Act
        var result = await _controller.ReorderAutoRoster(orgId, request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Waiver Endpoint Tests

    private static OrganizationWaiverDto CreateWaiverDto(Guid? orgId = null, int version = 1, string text = "Waiver text")
    {
        return new OrganizationWaiverDto(
            Guid.NewGuid(),
            orgId ?? Guid.NewGuid(),
            text,
            version,
            DateTime.UtcNow);
    }

    [Fact]
    public async Task GetWaiver_WithActiveWaiver_ReturnsOk()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var waiver = CreateWaiverDto(orgId);
        _mockWaiverService.Setup(s => s.GetCurrentWaiverAsync(orgId)).ReturnsAsync(waiver);

        // Act
        var result = await _controller.GetWaiver(orgId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(waiver);
    }

    [Fact]
    public async Task GetWaiver_WithNoActiveWaiver_Returns404()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        _mockWaiverService.Setup(s => s.GetCurrentWaiverAsync(orgId)).ReturnsAsync((OrganizationWaiverDto?)null);

        // Act
        var result = await _controller.GetWaiver(orgId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetWaiver_AsAdmin_ReturnsOkWithNewVersion()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var waiver = CreateWaiverDto(orgId, version: 2);
        _mockWaiverService.Setup(s => s.SetWaiverAsync(orgId, "New text", _testUserId)).ReturnsAsync(waiver);

        // Act
        var result = await _controller.SetWaiver(orgId, new SetOrganizationWaiverRequest("New text"));

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SetOrganizationWaiverResponse>().Subject;
        response.Waiver.Should().Be(waiver);
    }

    [Fact]
    public async Task SetWaiver_AsNonAdmin_Returns403()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        _mockWaiverService.Setup(s => s.SetWaiverAsync(orgId, It.IsAny<string?>(), _testUserId))
            .ThrowsAsync(new UnauthorizedAccessException("Only organization admins can update the waiver"));

        // Act
        var result = await _controller.SetWaiver(orgId, new SetOrganizationWaiverRequest("New text"));

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetWaiverPdf_WithActiveWaiver_ReturnsPdfFile()
    {
        // Arrange - endpoint is anonymous by design, no auth setup
        var orgId = Guid.NewGuid();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        _mockWaiverService.Setup(s => s.GetCurrentWaiverPdfAsync(orgId))
            .ReturnsAsync((pdfBytes, "test-org-waiver-v1.pdf"));

        // Act
        var result = await _controller.GetWaiverPdf(orgId);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be("test-org-waiver-v1.pdf");
        fileResult.FileContents.Should().Equal(pdfBytes);
    }

    [Fact]
    public async Task GetWaiverPdf_WithNoActiveWaiver_Returns404()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        _mockWaiverService.Setup(s => s.GetCurrentWaiverPdfAsync(orgId))
            .ReturnsAsync(((byte[], string)?)null);

        // Act
        var result = await _controller.GetWaiverPdf(orgId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AcceptWaiver_WithCurrentVersion_ReturnsOk()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var request = new AcceptWaiverRequest(Guid.NewGuid(), "Test Participant", DateTime.UtcNow.Date);

        // Act
        var result = await _controller.AcceptWaiver(orgId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        // The full request (waiver id + signature fields) is passed through
        _mockWaiverService.Verify(s => s.AcceptWaiverAsync(orgId, request, _testUserId), Times.Once);
    }

    [Fact]
    public async Task AcceptWaiver_WithStaleVersion_Returns400()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var request = new AcceptWaiverRequest(Guid.NewGuid(), "Test Participant", DateTime.UtcNow.Date);
        _mockWaiverService.Setup(s => s.AcceptWaiverAsync(orgId, request, _testUserId))
            .ThrowsAsync(new InvalidOperationException("This waiver version is no longer current."));

        // Act
        var result = await _controller.AcceptWaiver(orgId, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AcceptWaiver_WithValidationFailure_Returns400WithMessage()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        var request = new AcceptWaiverRequest(Guid.NewGuid(), "", DateTime.UtcNow.Date);
        _mockWaiverService.Setup(s => s.AcceptWaiverAsync(orgId, request, _testUserId))
            .ThrowsAsync(new InvalidOperationException("Printed name is required."));

        // Act
        var result = await _controller.AcceptWaiver(orgId, request);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { message = "Printed name is required." });
    }

    [Fact]
    public async Task Leave_DelegatesToServiceAndReturnsOk()
    {
        // Arrange
        SetupAuthenticatedUser(_testUserId);
        var orgId = Guid.NewGuid();
        _mockOrgService.Setup(s => s.LeaveAsync(orgId, _testUserId)).ReturnsAsync(true);

        // Act
        var result = await _controller.Leave(orgId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockOrgService.Verify(s => s.LeaveAsync(orgId, _testUserId), Times.Once);
    }

    #endregion

    #region Helper Methods

    private void SetupAuthenticatedUser(Guid userId, string role = "Organizer")
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private AutoRosterMemberDto CreateAutoRosterMemberDto(Guid? userId = null, int sortOrder = 0)
    {
        return new AutoRosterMemberDto(
            Guid.NewGuid(),
            userId ?? Guid.NewGuid(),
            "Test",
            "User",
            new Dictionary<string, string> { { "skater", "Silver" } },
            "Skater",
            sortOrder,
            DateTime.UtcNow
        );
    }

    private OrganizationDto CreateOrgDto(string name, bool isSubscribed = false, bool isCreator = false)
    {
        return new OrganizationDto(
            Guid.NewGuid(),
            name,
            "Description",
            "Location",
            new List<string> { "Gold" },
            Guid.NewGuid(),
            10,
            isSubscribed,
            isCreator,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );
    }

    #endregion
}
