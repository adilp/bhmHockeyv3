using System.Security.Claims;
using BHMHockey.Api.Controllers;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Controllers;

/// <summary>
/// Tests for EventsController - verifying event CRUD and registration endpoints.
/// </summary>
public class EventsControllerTests
{
    private readonly Mock<IEventService> _mockEventService;
    private readonly EventsController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testOrgId = Guid.NewGuid();
    private readonly Guid _testEventId = Guid.NewGuid();

    public EventsControllerTests()
    {
        _mockEventService = new Mock<IEventService>();
        _controller = new EventsController(_mockEventService.Object);
    }

    #region Test Helpers

    private void SetupAuthenticatedUser(Guid? userId = null)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, (userId ?? _testUserId).ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private void SetupUnauthenticatedUser()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private EventDto CreateEventDto(
        Guid? id = null,
        bool isRegistered = false,
        bool useDefaultOrg = true,
        Guid? organizationId = null,
        string? organizationName = "Test Organization",
        string visibility = "Public",
        bool isCreator = false)
    {
        // If useDefaultOrg is true and no explicit org is passed, use _testOrgId
        // If useDefaultOrg is false or explicit null is passed, use null
        var finalOrgId = useDefaultOrg && organizationId == null ? _testOrgId : organizationId;

        return new EventDto(
            Id: id ?? _testEventId,
            OrganizationId: finalOrgId,
            OrganizationName: organizationName,
            CreatorId: _testUserId,
            Name: "Test Event",
            Description: "Test Description",
            EventDate: DateTime.UtcNow.AddDays(7),
            Duration: 60,
            Venue: "Test Venue",
            MaxPlayers: 20,
            RegisteredCount: 5,
            Cost: 25.00m,
            RegistrationDeadline: DateTime.UtcNow.AddDays(6),
            Status: "Published",
            Visibility: visibility,
            SkillLevels: null,           // Multi-skill levels
            IsRegistered: isRegistered,
            CanManage: isCreator,
            CreatedAt: DateTime.UtcNow,
            CreatorVenmoHandle: null,    // Phase 4
            MyPaymentStatus: null,       // Phase 4
            MyTeamAssignment: null,      // Team assignment
            UnpaidCount: null            // Organizer view
        );
    }

    private UserDto CreateUserDto(Guid? id = null)
    {
        return new UserDto(
            Id: id ?? _testUserId,
            Email: "test@example.com",
            FirstName: "Test",
            LastName: "User",
            PhoneNumber: null,
            Positions: null,
            VenmoHandle: null,
            Role: "Player",
            CreatedAt: DateTime.UtcNow
        );
    }

    #endregion

    #region GetAll Tests

    [Fact]
    public async Task GetAll_WithNoAuth_ReturnsEventsWithoutRegistrationStatus()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var events = new List<EventDto> { CreateEventDto() };
        _mockEventService.Setup(s => s.GetAllAsync(null))
            .ReturnsAsync(events);

        // Act
        var result = await _controller.GetAll(null);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedEvents = okResult.Value.Should().BeAssignableTo<List<EventDto>>().Subject;
        returnedEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAll_WithAuth_ReturnsEventsWithRegistrationStatus()
    {
        // Arrange
        SetupAuthenticatedUser();
        var events = new List<EventDto> { CreateEventDto(isRegistered: true) };
        _mockEventService.Setup(s => s.GetAllAsync(_testUserId))
            .ReturnsAsync(events);

        // Act
        var result = await _controller.GetAll(null);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedEvents = okResult.Value.Should().BeAssignableTo<List<EventDto>>().Subject;
        returnedEvents.First().IsRegistered.Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_WithOrganizationId_ReturnsFilteredEvents()
    {
        // Arrange
        SetupAuthenticatedUser();
        var events = new List<EventDto> { CreateEventDto() };
        _mockEventService.Setup(s => s.GetByOrganizationAsync(_testOrgId, _testUserId))
            .ReturnsAsync(events);

        // Act
        var result = await _controller.GetAll(_testOrgId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        _mockEventService.Verify(s => s.GetByOrganizationAsync(_testOrgId, _testUserId), Times.Once);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetById_WithValidId_ReturnsEvent()
    {
        // Arrange
        SetupAuthenticatedUser();
        var eventDto = CreateEventDto();
        _mockEventService.Setup(s => s.GetByIdAsync(_testEventId, _testUserId))
            .ReturnsAsync(eventDto);

        // Act
        var result = await _controller.GetById(_testEventId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedEvent = okResult.Value.Should().BeOfType<EventDto>().Subject;
        returnedEvent.Id.Should().Be(_testEventId);
    }

    [Fact]
    public async Task GetById_WithInvalidId_Returns404()
    {
        // Arrange
        SetupAuthenticatedUser();
        _mockEventService.Setup(s => s.GetByIdAsync(_testEventId, _testUserId))
            .ReturnsAsync((EventDto?)null);

        // Act
        var result = await _controller.GetById(_testEventId);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedEvent()
    {
        // Arrange
        SetupAuthenticatedUser();
        var request = new CreateEventRequest(
            OrganizationId: _testOrgId,
            Name: "New Event",
            Description: "Description",
            EventDate: DateTime.UtcNow.AddDays(7),
            Duration: 90,
            Venue: "Test Venue",
            MaxPlayers: 15,
            Cost: 30.00m,
            RegistrationDeadline: DateTime.UtcNow.AddDays(6)
        );
        var createdEvent = CreateEventDto();
        _mockEventService.Setup(s => s.CreateAsync(request, _testUserId))
            .ReturnsAsync(createdEvent);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(_controller.GetById));
        var returnedEvent = createdResult.Value.Should().BeOfType<EventDto>().Subject;
        returnedEvent.Should().NotBeNull();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_AsCreator_ReturnsUpdatedEvent()
    {
        // Arrange
        SetupAuthenticatedUser();
        var request = new UpdateEventRequest(
            Name: "Updated Name",
            Description: null,
            EventDate: null,
            Duration: null,
            Venue: null,
            MaxPlayers: null,
            Cost: null,
            RegistrationDeadline: null,
            Status: null,
            Visibility: null,
            SkillLevels: null
        );
        var updatedEvent = CreateEventDto();
        _mockEventService.Setup(s => s.UpdateAsync(_testEventId, request, _testUserId))
            .ReturnsAsync(updatedEvent);

        // Act
        var result = await _controller.Update(_testEventId, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<EventDto>();
    }

    [Fact]
    public async Task Update_AsNonCreator_Returns404()
    {
        // Arrange
        SetupAuthenticatedUser();
        var request = new UpdateEventRequest("Updated", null, null, null, null, null, null, null, null, null, null);
        _mockEventService.Setup(s => s.UpdateAsync(_testEventId, request, _testUserId))
            .ReturnsAsync((EventDto?)null);

        // Act
        var result = await _controller.Update(_testEventId, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_AsCreator_Returns204NoContent()
    {
        // Arrange
        SetupAuthenticatedUser();
        _mockEventService.Setup(s => s.DeleteAsync(_testEventId, _testUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(_testEventId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_AsNonCreator_Returns404()
    {
        // Arrange
        SetupAuthenticatedUser();
        _mockEventService.Setup(s => s.DeleteAsync(_testEventId, _testUserId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Delete(_testEventId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Standalone Event Tests

    [Fact]
    public async Task Create_StandaloneEvent_WithNoOrganization_ReturnsCreatedEvent()
    {
        // Arrange
        SetupAuthenticatedUser();
        var request = new CreateEventRequest(
            OrganizationId: null, // No organization - standalone pickup game
            Name: "Pickup Hockey",
            Description: "Casual game",
            EventDate: DateTime.UtcNow.AddDays(7),
            Duration: 90,
            Venue: "Local Rink",
            MaxPlayers: 12,
            Cost: 10.00m,
            RegistrationDeadline: null,
            Visibility: "Public"
        );
        var createdEvent = CreateEventDto(useDefaultOrg: false, organizationName: null);
        _mockEventService.Setup(s => s.CreateAsync(request, _testUserId))
            .ReturnsAsync(createdEvent);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var returnedEvent = createdResult.Value.Should().BeOfType<EventDto>().Subject;
        returnedEvent.OrganizationId.Should().BeNull();
    }

    [Fact]
    public async Task Create_InviteOnlyEvent_ReturnsCreatedEvent()
    {
        // Arrange
        SetupAuthenticatedUser();
        var request = new CreateEventRequest(
            OrganizationId: null,
            Name: "Private Game",
            Description: "Invite only",
            EventDate: DateTime.UtcNow.AddDays(7),
            Duration: 60,
            Venue: "Private Rink",
            MaxPlayers: 10,
            Cost: 0m,
            RegistrationDeadline: null,
            Visibility: "InviteOnly"
        );
        var createdEvent = CreateEventDto(useDefaultOrg: false, organizationName: null, visibility: "InviteOnly", isCreator: true);
        _mockEventService.Setup(s => s.CreateAsync(request, _testUserId))
            .ReturnsAsync(createdEvent);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var returnedEvent = createdResult.Value.Should().BeOfType<EventDto>().Subject;
        returnedEvent.Visibility.Should().Be("InviteOnly");
    }

    #endregion

    #region Registration Tests

    [Fact]
    public async Task Register_WhenNotRegistered_ReturnsSuccess()
    {
        // Arrange
        SetupAuthenticatedUser();
        _mockEventService.Setup(s => s.RegisterAsync(_testEventId, _testUserId, null))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Register(_testEventId, null);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_WhenAlreadyRegistered_Returns400()
    {
        // Arrange
        SetupAuthenticatedUser();
        _mockEventService.Setup(s => s.RegisterAsync(_testEventId, _testUserId, null))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Register(_testEventId, null);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_WhenEventFull_Returns400()
    {
        // Arrange
        SetupAuthenticatedUser();
        _mockEventService.Setup(s => s.RegisterAsync(_testEventId, _testUserId, null))
            .ThrowsAsync(new InvalidOperationException("Event is full"));

        // Act
        var result = await _controller.Register(_testEventId, null);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_WithPosition_PassesPositionToService()
    {
        // Arrange
        SetupAuthenticatedUser();
        var request = new RegisterForEventRequest("Goalie");
        _mockEventService.Setup(s => s.RegisterAsync(_testEventId, _testUserId, "Goalie"))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Register(_testEventId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockEventService.Verify(s => s.RegisterAsync(_testEventId, _testUserId, "Goalie"), Times.Once);
    }

    [Fact]
    public async Task CancelRegistration_WhenRegistered_Returns204()
    {
        // Arrange
        SetupAuthenticatedUser();
        _mockEventService.Setup(s => s.CancelRegistrationAsync(_testEventId, _testUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.CancelRegistration(_testEventId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task CancelRegistration_WhenNotRegistered_Returns404()
    {
        // Arrange
        SetupAuthenticatedUser();
        _mockEventService.Setup(s => s.CancelRegistrationAsync(_testEventId, _testUserId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.CancelRegistration(_testEventId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetRegistrations_ReturnsListOfRegistrations()
    {
        // Arrange
        SetupAuthenticatedUser();
        var registrations = new List<EventRegistrationDto>
        {
            new EventRegistrationDto(
                Id: Guid.NewGuid(),
                EventId: _testEventId,
                User: CreateUserDto(),
                Status: "Registered",
                RegisteredAt: DateTime.UtcNow,
                RegisteredPosition: "Skater",  // Position tracking
                PaymentStatus: null,           // Phase 4
                PaymentMarkedAt: null,         // Phase 4
                PaymentVerifiedAt: null,       // Phase 4
                TeamAssignment: null           // Team assignment
            )
        };
        _mockEventService.Setup(s => s.GetRegistrationsAsync(_testEventId))
            .ReturnsAsync(registrations);

        // Act
        var result = await _controller.GetRegistrations(_testEventId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedRegistrations = okResult.Value.Should().BeAssignableTo<List<EventRegistrationDto>>().Subject;
        returnedRegistrations.Should().HaveCount(1);
    }

    #endregion

    #region Payment Tests

    [Fact]
    public async Task MarkPayment_WithValidRegistration_ReturnsOk()
    {
        // Arrange
        SetupAuthenticatedUser();
        _mockEventService.Setup(s => s.MarkPaymentAsync(_testEventId, _testUserId, null))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.MarkPayment(_testEventId, null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task MarkPayment_WhenNotRegistered_Returns400()
    {
        // Arrange
        SetupAuthenticatedUser();
        _mockEventService.Setup(s => s.MarkPaymentAsync(_testEventId, _testUserId, null))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.MarkPayment(_testEventId, null);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task MarkPayment_ForFreeEvent_Returns400()
    {
        // Arrange
        SetupAuthenticatedUser();
        // Service returns false for free events (Cost <= 0)
        _mockEventService.Setup(s => s.MarkPaymentAsync(_testEventId, _testUserId, null))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.MarkPayment(_testEventId, null);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task MarkPayment_WhenAlreadyMarkedPaid_Returns400()
    {
        // Arrange
        SetupAuthenticatedUser();
        // Service returns false when PaymentStatus is not "Pending"
        _mockEventService.Setup(s => s.MarkPaymentAsync(_testEventId, _testUserId, null))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.MarkPayment(_testEventId, null);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdatePaymentStatus_AsOrganizer_ToVerified_ReturnsOk()
    {
        // Arrange
        SetupAuthenticatedUser();
        var registrationId = Guid.NewGuid();
        var request = new UpdatePaymentStatusRequest("Verified");
        _mockEventService.Setup(s => s.UpdatePaymentStatusAsync(_testEventId, registrationId, "Verified", _testUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.UpdatePaymentStatus(_testEventId, registrationId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdatePaymentStatus_AsOrganizer_ToPending_ReturnsOk()
    {
        // Arrange
        SetupAuthenticatedUser();
        var registrationId = Guid.NewGuid();
        var request = new UpdatePaymentStatusRequest("Pending");
        _mockEventService.Setup(s => s.UpdatePaymentStatusAsync(_testEventId, registrationId, "Pending", _testUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.UpdatePaymentStatus(_testEventId, registrationId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdatePaymentStatus_AsNonOrganizer_Returns404()
    {
        // Arrange
        SetupAuthenticatedUser();
        var registrationId = Guid.NewGuid();
        var request = new UpdatePaymentStatusRequest("Verified");
        // Service returns false when user is not the event creator
        _mockEventService.Setup(s => s.UpdatePaymentStatusAsync(_testEventId, registrationId, "Verified", _testUserId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.UpdatePaymentStatus(_testEventId, registrationId, request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdatePaymentStatus_WithInvalidStatus_Returns400()
    {
        // Arrange
        SetupAuthenticatedUser();
        var registrationId = Guid.NewGuid();
        var request = new UpdatePaymentStatusRequest("InvalidStatus");
        _mockEventService.Setup(s => s.UpdatePaymentStatusAsync(_testEventId, registrationId, "InvalidStatus", _testUserId))
            .ThrowsAsync(new InvalidOperationException("Invalid payment status. Must be 'Verified' or 'Pending'"));

        // Act
        var result = await _controller.UpdatePaymentStatus(_testEventId, registrationId, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
