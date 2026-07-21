using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for OrganizationWaiverService - protecting the legal waiver rules:
/// - Versions are immutable and per-org incrementing (audit trail)
/// - "Active waiver" = latest version row IF its text is non-empty
/// - Acceptances are idempotent and stale versions are rejected
/// - Pending waivers cover only upcoming, non-cancelled org events
/// </summary>
public class OrganizationWaiverServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly OrganizationAdminService _adminService;
    private readonly OrganizationWaiverService _sut;

    public OrganizationWaiverServiceTests()
    {
        // QuestPDF requires a license selection before rendering (set in Program.cs at runtime)
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _adminService = new OrganizationAdminService(_context);
        _sut = new OrganizationWaiverService(_context, _adminService, Mock.Of<ILogger<OrganizationWaiverService>>());
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private async Task<User> CreateTestUser(string email = "test@example.com", bool isGhost = false)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_password",
            FirstName = "Test",
            LastName = "User",
            Positions = new Dictionary<string, string> { { "skater", "Silver" } },
            Role = "Player",
            IsActive = true,
            IsGhostPlayer = isGhost,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Organization> CreateTestOrganization(Guid creatorId, string name = "Test Org", bool isActive = true)
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatorId = creatorId,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(org);
        _context.OrganizationAdmins.Add(new OrganizationAdmin
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            UserId = creatorId,
            AddedAt = DateTime.UtcNow,
            AddedByUserId = null
        });

        await _context.SaveChangesAsync();
        return org;
    }

    private async Task<Event> CreateTestEvent(
        Guid creatorId,
        Guid? organizationId,
        DateTime? eventDate = null,
        string status = "Published")
    {
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            OrganizationId = organizationId,
            Name = "Test Event",
            EventDate = eventDate ?? DateTime.UtcNow.AddDays(7),
            MaxPlayers = 10,
            Cost = 0,
            Status = status,
            Visibility = "Public"
        };

        _context.Events.Add(evt);
        await _context.SaveChangesAsync();
        return evt;
    }

    private async Task<EventRegistration> CreateRegistration(Guid eventId, Guid userId, string status = "Registered")
    {
        var registration = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = status,
            RegisteredPosition = "Skater",
            WaitlistPosition = status == "Waitlisted" ? 1 : null
        };

        _context.EventRegistrations.Add(registration);
        await _context.SaveChangesAsync();
        return registration;
    }

    /// <summary>
    /// Minimal valid acceptance request: adult signature fields only.
    /// </summary>
    private static AcceptWaiverRequest ValidAcceptRequest(Guid waiverId, string participantName = "Test Participant")
    {
        return new AcceptWaiverRequest(waiverId, participantName, DateTime.UtcNow.Date);
    }

    /// <summary>
    /// Valid acceptance request with the full Parent/Guardian section filled in.
    /// </summary>
    private static AcceptWaiverRequest MinorAcceptRequest(Guid waiverId)
    {
        return new AcceptWaiverRequest(
            waiverId,
            "Guardian As Participant",
            DateTime.UtcNow.Date,
            MinorParticipantName: "Minor Player",
            MinorDateOfBirth: DateTime.UtcNow.Date.AddYears(-12),
            GuardianName: "Parent Guardian",
            GuardianSignature: "Parent Guardian",
            GuardianDate: DateTime.UtcNow.Date);
    }

    #endregion

    #region SetWaiver / Versioning Tests

    [Fact]
    public async Task SetWaiverAsync_FirstSave_CreatesVersion1()
    {
        var admin = await CreateTestUser();
        var org = await CreateTestOrganization(admin.Id);

        var result = await _sut.SetWaiverAsync(org.Id, "You play at your own risk.", admin.Id);

        result.Should().NotBeNull();
        result!.Version.Should().Be(1);
        result.Text.Should().Be("You play at your own risk.");
        result.OrganizationId.Should().Be(org.Id);
    }

    [Fact]
    public async Task SetWaiverAsync_Edit_CreatesNewRowAndLeavesOldRowUntouched()
    {
        var admin = await CreateTestUser();
        var org = await CreateTestOrganization(admin.Id);
        var v1 = await _sut.SetWaiverAsync(org.Id, "Version one text", admin.Id);

        var v2 = await _sut.SetWaiverAsync(org.Id, "Version two text", admin.Id);

        v2!.Version.Should().Be(2);
        v2.Id.Should().NotBe(v1!.Id);

        // Immutability: the v1 row still exists with byte-identical text
        var rows = await _context.OrganizationWaivers
            .Where(w => w.OrganizationId == org.Id)
            .OrderBy(w => w.Version)
            .ToListAsync();
        rows.Should().HaveCount(2);
        rows[0].Id.Should().Be(v1.Id);
        rows[0].Text.Should().Be("Version one text");
        rows[0].Version.Should().Be(1);
    }

    [Fact]
    public async Task SetWaiverAsync_TrimsText()
    {
        var admin = await CreateTestUser();
        var org = await CreateTestOrganization(admin.Id);

        var result = await _sut.SetWaiverAsync(org.Id, "   padded text \n ", admin.Id);

        result!.Text.Should().Be("padded text");
    }

    [Fact]
    public async Task SetWaiverAsync_VersionsIncrementPerOrg()
    {
        var admin = await CreateTestUser();
        var orgA = await CreateTestOrganization(admin.Id, "Org A");
        var orgB = await CreateTestOrganization(admin.Id, "Org B");

        await _sut.SetWaiverAsync(orgA.Id, "A v1", admin.Id);
        await _sut.SetWaiverAsync(orgA.Id, "A v2", admin.Id);
        var bResult = await _sut.SetWaiverAsync(orgB.Id, "B v1", admin.Id);

        // Org B starts at version 1 regardless of org A's history
        bResult!.Version.Should().Be(1);
    }

    [Fact]
    public async Task SetWaiverAsync_NonAdmin_ThrowsUnauthorized()
    {
        var admin = await CreateTestUser();
        var nonAdmin = await CreateTestUser("other@example.com");
        var org = await CreateTestOrganization(admin.Id);

        var act = () => _sut.SetWaiverAsync(org.Id, "text", nonAdmin.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await _context.OrganizationWaivers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SetWaiverAsync_EmptyText_CreatesDeactivatingVersionAndPreservesHistory()
    {
        var admin = await CreateTestUser();
        var org = await CreateTestOrganization(admin.Id);
        await _sut.SetWaiverAsync(org.Id, "Some waiver", admin.Id);

        var result = await _sut.SetWaiverAsync(org.Id, "   ", admin.Id);

        result.Should().BeNull(); // No active waiver after clearing
        var rows = await _context.OrganizationWaivers
            .Where(w => w.OrganizationId == org.Id)
            .OrderBy(w => w.Version)
            .ToListAsync();
        rows.Should().HaveCount(2);
        rows[0].Text.Should().Be("Some waiver"); // History preserved
        rows[1].Text.Should().Be("");
        rows[1].Version.Should().Be(2);
    }

    [Fact]
    public async Task SetWaiverAsync_EmptyTextWithNoExistingWaiver_IsNoOp()
    {
        var admin = await CreateTestUser();
        var org = await CreateTestOrganization(admin.Id);

        var result = await _sut.SetWaiverAsync(org.Id, "", admin.Id);

        result.Should().BeNull();
        (await _context.OrganizationWaivers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SetWaiverAsync_IdenticalText_DoesNotCreateNewVersion()
    {
        var admin = await CreateTestUser();
        var org = await CreateTestOrganization(admin.Id);
        var v1 = await _sut.SetWaiverAsync(org.Id, "Same text", admin.Id);

        var result = await _sut.SetWaiverAsync(org.Id, "Same text", admin.Id);

        result!.Id.Should().Be(v1!.Id);
        result.Version.Should().Be(1);
        (await _context.OrganizationWaivers.CountAsync()).Should().Be(1);
    }

    #endregion

    #region Active Waiver Semantics

    [Fact]
    public async Task GetCurrentWaiverAsync_NoWaiver_ReturnsNull()
    {
        var admin = await CreateTestUser();
        var org = await CreateTestOrganization(admin.Id);

        var result = await _sut.GetCurrentWaiverAsync(org.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentWaiverAsync_ReturnsLatestVersion()
    {
        var admin = await CreateTestUser();
        var org = await CreateTestOrganization(admin.Id);
        await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        var v2 = await _sut.SetWaiverAsync(org.Id, "v2", admin.Id);

        var result = await _sut.GetCurrentWaiverAsync(org.Id);

        result!.Id.Should().Be(v2!.Id);
        result.Version.Should().Be(2);
        result.Text.Should().Be("v2");
    }

    [Fact]
    public async Task GetCurrentWaiverAsync_ClearedWaiver_ReturnsNull()
    {
        var admin = await CreateTestUser();
        var org = await CreateTestOrganization(admin.Id);
        await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        await _sut.SetWaiverAsync(org.Id, "", admin.Id);

        var result = await _sut.GetCurrentWaiverAsync(org.Id);

        result.Should().BeNull();
        (await _sut.GetActiveWaiverIdAsync(org.Id)).Should().BeNull();
    }

    [Fact]
    public async Task IsAcceptanceRequiredAsync_TracksActiveWaiverAndAcceptance()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);

        // No waiver -> not required
        (await _sut.IsAcceptanceRequiredAsync(org.Id, player.Id)).Should().BeFalse();

        // Active waiver, not accepted -> required
        var v1 = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        (await _sut.IsAcceptanceRequiredAsync(org.Id, player.Id)).Should().BeTrue();

        // Accepted -> not required
        await _sut.AcceptWaiverAsync(org.Id, ValidAcceptRequest(v1!.Id), player.Id);
        (await _sut.IsAcceptanceRequiredAsync(org.Id, player.Id)).Should().BeFalse();

        // New version -> required again
        await _sut.SetWaiverAsync(org.Id, "v2", admin.Id);
        (await _sut.IsAcceptanceRequiredAsync(org.Id, player.Id)).Should().BeTrue();

        // Cleared -> not required
        await _sut.SetWaiverAsync(org.Id, "", admin.Id);
        (await _sut.IsAcceptanceRequiredAsync(org.Id, player.Id)).Should().BeFalse();
    }

    #endregion

    #region Acceptance Tests

    [Fact]
    public async Task AcceptWaiverAsync_RecordsAcceptance()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);

        await _sut.AcceptWaiverAsync(org.Id, ValidAcceptRequest(waiver!.Id), player.Id);

        var acceptance = await _context.WaiverAcceptances
            .FirstOrDefaultAsync(a => a.WaiverId == waiver.Id && a.UserId == player.Id);
        acceptance.Should().NotBeNull();
        acceptance!.AcceptedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task AcceptWaiverAsync_AlreadyAccepted_IsIdempotent()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);

        await _sut.AcceptWaiverAsync(org.Id, ValidAcceptRequest(waiver!.Id), player.Id);
        await _sut.AcceptWaiverAsync(org.Id, ValidAcceptRequest(waiver.Id), player.Id); // No throw

        (await _context.WaiverAcceptances.CountAsync(a => a.WaiverId == waiver.Id && a.UserId == player.Id))
            .Should().Be(1);
    }

    [Fact]
    public async Task AcceptWaiverAsync_StaleVersion_Throws()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var v1 = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        await _sut.SetWaiverAsync(org.Id, "v2", admin.Id);

        var act = () => _sut.AcceptWaiverAsync(org.Id, ValidAcceptRequest(v1!.Id), player.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no longer current*");
        (await _context.WaiverAcceptances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AcceptWaiverAsync_ClearedWaiver_Throws()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var v1 = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        await _sut.SetWaiverAsync(org.Id, "", admin.Id);

        var act = () => _sut.AcceptWaiverAsync(org.Id, ValidAcceptRequest(v1!.Id), player.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AcceptWaiverAsync_WaiverFromDifferentOrg_Throws()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var orgA = await CreateTestOrganization(admin.Id, "Org A");
        var orgB = await CreateTestOrganization(admin.Id, "Org B");
        var waiverA = await _sut.SetWaiverAsync(orgA.Id, "A v1", admin.Id);
        await _sut.SetWaiverAsync(orgB.Id, "B v1", admin.Id);

        var act = () => _sut.AcceptWaiverAsync(orgB.Id, ValidAcceptRequest(waiverA!.Id), player.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Signature Field Tests

    [Fact]
    public async Task AcceptWaiverAsync_AdultOnly_StoresParticipantFieldsAndLeavesMinorFieldsNull()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        var today = DateTime.UtcNow.Date;

        await _sut.AcceptWaiverAsync(
            org.Id, new AcceptWaiverRequest(waiver!.Id, "Jane Skater", today), player.Id);

        var acceptance = await _context.WaiverAcceptances
            .SingleAsync(a => a.WaiverId == waiver.Id && a.UserId == player.Id);
        acceptance.ParticipantName.Should().Be("Jane Skater");
        acceptance.ParticipantDate.Should().Be(today);
        acceptance.ParticipantDate!.Value.Kind.Should().Be(DateTimeKind.Utc);
        acceptance.MinorParticipantName.Should().BeNull();
        acceptance.MinorDateOfBirth.Should().BeNull();
        acceptance.GuardianName.Should().BeNull();
        acceptance.GuardianSignature.Should().BeNull();
        acceptance.GuardianDate.Should().BeNull();
    }

    [Fact]
    public async Task AcceptWaiverAsync_FullMinorSection_StoresAllSignatureFields()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        var request = MinorAcceptRequest(waiver!.Id);

        await _sut.AcceptWaiverAsync(org.Id, request, player.Id);

        var acceptance = await _context.WaiverAcceptances
            .SingleAsync(a => a.WaiverId == waiver.Id && a.UserId == player.Id);
        acceptance.ParticipantName.Should().Be(request.ParticipantName);
        acceptance.ParticipantDate.Should().Be(request.ParticipantDate);
        acceptance.MinorParticipantName.Should().Be(request.MinorParticipantName);
        acceptance.MinorDateOfBirth.Should().Be(request.MinorDateOfBirth);
        acceptance.GuardianName.Should().Be(request.GuardianName);
        acceptance.GuardianSignature.Should().Be(request.GuardianSignature);
        acceptance.GuardianDate.Should().Be(request.GuardianDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AcceptWaiverAsync_MissingOrBlankParticipantName_Throws(string? participantName)
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);

        var act = () => _sut.AcceptWaiverAsync(
            org.Id, new AcceptWaiverRequest(waiver!.Id, participantName, DateTime.UtcNow.Date), player.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Printed name is required.");
        (await _context.WaiverAcceptances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AcceptWaiverAsync_MissingParticipantDate_Throws()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);

        var act = () => _sut.AcceptWaiverAsync(
            org.Id, new AcceptWaiverRequest(waiver!.Id, "Jane Skater", null), player.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Date is required.");
        (await _context.WaiverAcceptances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AcceptWaiverAsync_ParticipantNameTooLong_Throws()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);

        var act = () => _sut.AcceptWaiverAsync(
            org.Id, new AcceptWaiverRequest(waiver!.Id, new string('x', 201), DateTime.UtcNow.Date), player.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*200 characters or fewer*");
    }

    [Fact]
    public async Task AcceptWaiverAsync_TrimsSignatureStrings()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        var today = DateTime.UtcNow.Date;

        await _sut.AcceptWaiverAsync(
            org.Id,
            new AcceptWaiverRequest(
                waiver!.Id, "  Jane Skater  ", today,
                MinorParticipantName: " Minor Player ",
                MinorDateOfBirth: today.AddYears(-12),
                GuardianName: " Parent Guardian ",
                GuardianSignature: " Parent Guardian ",
                GuardianDate: today),
            player.Id);

        var acceptance = await _context.WaiverAcceptances
            .SingleAsync(a => a.WaiverId == waiver.Id && a.UserId == player.Id);
        acceptance.ParticipantName.Should().Be("Jane Skater");
        acceptance.MinorParticipantName.Should().Be("Minor Player");
        acceptance.GuardianName.Should().Be("Parent Guardian");
        acceptance.GuardianSignature.Should().Be("Parent Guardian");
    }

    // All-or-nothing: each single filled minor field must reject the request
    [Theory]
    [InlineData("minorName")]
    [InlineData("minorDob")]
    [InlineData("guardianName")]
    [InlineData("guardianSignature")]
    [InlineData("guardianDate")]
    public async Task AcceptWaiverAsync_PartialMinorSection_Throws(string filledField)
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        var today = DateTime.UtcNow.Date;
        var request = new AcceptWaiverRequest(
            waiver!.Id, "Jane Skater", today,
            MinorParticipantName: filledField == "minorName" ? "Minor Player" : null,
            MinorDateOfBirth: filledField == "minorDob" ? today.AddYears(-12) : null,
            GuardianName: filledField == "guardianName" ? "Parent Guardian" : null,
            GuardianSignature: filledField == "guardianSignature" ? "Parent Guardian" : null,
            GuardianDate: filledField == "guardianDate" ? today : null);

        var act = () => _sut.AcceptWaiverAsync(org.Id, request, player.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*all-or-nothing*");
        (await _context.WaiverAcceptances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AcceptWaiverAsync_WhitespaceOnlyMinorSection_TreatedAsEmpty()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);

        // Whitespace-only strings do not activate the all-or-nothing group
        await _sut.AcceptWaiverAsync(
            org.Id,
            new AcceptWaiverRequest(
                waiver!.Id, "Jane Skater", DateTime.UtcNow.Date,
                MinorParticipantName: "   ",
                GuardianName: " "),
            player.Id);

        var acceptance = await _context.WaiverAcceptances
            .SingleAsync(a => a.WaiverId == waiver.Id && a.UserId == player.Id);
        acceptance.MinorParticipantName.Should().BeNull();
        acceptance.GuardianName.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]   // today is not "in the past"
    [InlineData(30)]  // future date
    public async Task AcceptWaiverAsync_MinorDateOfBirthNotInPast_Throws(int daysFromToday)
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        var request = MinorAcceptRequest(waiver!.Id) with
        {
            MinorDateOfBirth = DateTime.UtcNow.Date.AddDays(daysFromToday)
        };

        var act = () => _sut.AcceptWaiverAsync(org.Id, request, player.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*date of birth must be in the past*");
        (await _context.WaiverAcceptances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AcceptWaiverAsync_ReacceptWithDifferentFields_PreservesOriginalRow()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        var firstDate = DateTime.UtcNow.Date.AddDays(-1);

        await _sut.AcceptWaiverAsync(
            org.Id, new AcceptWaiverRequest(waiver!.Id, "Original Name", firstDate), player.Id);
        // Re-accept the same version with different signature details
        await _sut.AcceptWaiverAsync(
            org.Id, MinorAcceptRequest(waiver.Id), player.Id);

        // Immutable audit record: still one row, exactly as first recorded
        var acceptances = await _context.WaiverAcceptances
            .Where(a => a.WaiverId == waiver.Id && a.UserId == player.Id)
            .ToListAsync();
        acceptances.Should().HaveCount(1);
        acceptances[0].ParticipantName.Should().Be("Original Name");
        acceptances[0].ParticipantDate.Should().Be(firstDate);
        acceptances[0].MinorParticipantName.Should().BeNull();
        acceptances[0].GuardianSignature.Should().BeNull();
    }

    [Fact]
    public async Task AcceptWaiverAsync_NewVersion_RecordsNewSignatureFieldsAndKeepsOldRow()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var v1 = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        await _sut.AcceptWaiverAsync(
            org.Id, new AcceptWaiverRequest(v1!.Id, "Old Name", DateTime.UtcNow.Date.AddDays(-30)), player.Id);

        var v2 = await _sut.SetWaiverAsync(org.Id, "v2", admin.Id);
        await _sut.AcceptWaiverAsync(
            org.Id, new AcceptWaiverRequest(v2!.Id, "New Name", DateTime.UtcNow.Date), player.Id);

        var oldRow = await _context.WaiverAcceptances.SingleAsync(a => a.WaiverId == v1.Id && a.UserId == player.Id);
        var newRow = await _context.WaiverAcceptances.SingleAsync(a => a.WaiverId == v2.Id && a.UserId == player.Id);
        oldRow.ParticipantName.Should().Be("Old Name");
        newRow.ParticipantName.Should().Be("New Name");
    }

    [Fact]
    public async Task AcceptWaiverAsync_UnspecifiedKindDates_NormalizedToUtcMidnight()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        // JSON date-only values ("2026-07-21") bind with Kind=Unspecified and may
        // carry a time component from other clients - both must be normalized
        var unspecified = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(14), DateTimeKind.Unspecified);

        await _sut.AcceptWaiverAsync(
            org.Id, new AcceptWaiverRequest(waiver!.Id, "Jane Skater", unspecified), player.Id);

        var acceptance = await _context.WaiverAcceptances
            .SingleAsync(a => a.WaiverId == waiver.Id && a.UserId == player.Id);
        acceptance.ParticipantDate.Should().Be(unspecified.Date);
        acceptance.ParticipantDate!.Value.Kind.Should().Be(DateTimeKind.Utc);
        acceptance.ParticipantDate.Value.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    #endregion

    #region Pending Waivers Tests

    [Fact]
    public async Task GetPendingWaiversAsync_ListsOrgWithUnacceptedWaiverOnUpcomingEvent()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id, "Waiver Org");
        var waiver = await _sut.SetWaiverAsync(org.Id, "waiver text", admin.Id);
        var evt = await CreateTestEvent(admin.Id, org.Id);
        await CreateRegistration(evt.Id, player.Id);

        var pending = await _sut.GetPendingWaiversAsync(player.Id);

        pending.Should().HaveCount(1);
        pending[0].OrganizationId.Should().Be(org.Id);
        pending[0].OrganizationName.Should().Be("Waiver Org");
        pending[0].Waiver.Id.Should().Be(waiver!.Id);
        pending[0].Waiver.Text.Should().Be("waiver text");
    }

    [Fact]
    public async Task GetPendingWaiversAsync_WaitlistedRegistrationCounts()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.SetWaiverAsync(org.Id, "waiver text", admin.Id);
        var evt = await CreateTestEvent(admin.Id, org.Id);
        await CreateRegistration(evt.Id, player.Id, status: "Waitlisted");

        var pending = await _sut.GetPendingWaiversAsync(player.Id);

        pending.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPendingWaiversAsync_AcceptedUser_NotListed()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var waiver = await _sut.SetWaiverAsync(org.Id, "waiver text", admin.Id);
        var evt = await CreateTestEvent(admin.Id, org.Id);
        await CreateRegistration(evt.Id, player.Id);
        await _sut.AcceptWaiverAsync(org.Id, ValidAcceptRequest(waiver!.Id), player.Id);

        var pending = await _sut.GetPendingWaiversAsync(player.Id);

        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingWaiversAsync_NewVersionAfterAcceptance_ListedAgain()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        var v1 = await _sut.SetWaiverAsync(org.Id, "v1", admin.Id);
        var evt = await CreateTestEvent(admin.Id, org.Id);
        await CreateRegistration(evt.Id, player.Id);
        await _sut.AcceptWaiverAsync(org.Id, ValidAcceptRequest(v1!.Id), player.Id);

        var v2 = await _sut.SetWaiverAsync(org.Id, "v2", admin.Id);
        var pending = await _sut.GetPendingWaiversAsync(player.Id);

        pending.Should().HaveCount(1);
        pending[0].Waiver.Id.Should().Be(v2!.Id);
        pending[0].Waiver.Version.Should().Be(2);
    }

    [Fact]
    public async Task GetPendingWaiversAsync_PastEvent_Excluded()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.SetWaiverAsync(org.Id, "waiver text", admin.Id);
        var pastEvent = await CreateTestEvent(admin.Id, org.Id, eventDate: DateTime.UtcNow.AddDays(-2));
        await CreateRegistration(pastEvent.Id, player.Id);

        var pending = await _sut.GetPendingWaiversAsync(player.Id);

        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingWaiversAsync_CancelledEvent_Excluded()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.SetWaiverAsync(org.Id, "waiver text", admin.Id);
        var cancelledEvent = await CreateTestEvent(admin.Id, org.Id, status: "Cancelled");
        await CreateRegistration(cancelledEvent.Id, player.Id);

        var pending = await _sut.GetPendingWaiversAsync(player.Id);

        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingWaiversAsync_CancelledRegistration_Excluded()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.SetWaiverAsync(org.Id, "waiver text", admin.Id);
        var evt = await CreateTestEvent(admin.Id, org.Id);
        await CreateRegistration(evt.Id, player.Id, status: "Cancelled");

        var pending = await _sut.GetPendingWaiversAsync(player.Id);

        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingWaiversAsync_NoActiveWaiver_Excluded()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.SetWaiverAsync(org.Id, "waiver text", admin.Id);
        await _sut.SetWaiverAsync(org.Id, "", admin.Id); // Cleared
        var evt = await CreateTestEvent(admin.Id, org.Id);
        await CreateRegistration(evt.Id, player.Id);

        var pending = await _sut.GetPendingWaiversAsync(player.Id);

        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingWaiversAsync_StandaloneEvent_Excluded()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var standaloneEvent = await CreateTestEvent(admin.Id, organizationId: null);
        await CreateRegistration(standaloneEvent.Id, player.Id);

        var pending = await _sut.GetPendingWaiversAsync(player.Id);

        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingWaiversAsync_MultipleEventsSameOrg_ListedOnce()
    {
        var admin = await CreateTestUser();
        var player = await CreateTestUser("player@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await _sut.SetWaiverAsync(org.Id, "waiver text", admin.Id);
        var evt1 = await CreateTestEvent(admin.Id, org.Id);
        var evt2 = await CreateTestEvent(admin.Id, org.Id);
        await CreateRegistration(evt1.Id, player.Id);
        await CreateRegistration(evt2.Id, player.Id);

        var pending = await _sut.GetPendingWaiversAsync(player.Id);

        pending.Should().HaveCount(1);
    }

    #endregion

    #region PDF Tests

    [Fact]
    public async Task GetCurrentWaiverPdfAsync_ActiveWaiver_ReturnsPdfBytes()
    {
        var admin = await CreateTestUser();
        var org = await CreateTestOrganization(admin.Id, "PDF Test Org");
        await _sut.SetWaiverAsync(org.Id, "This is the waiver body text used to render the PDF.", admin.Id);

        var result = await _sut.GetCurrentWaiverPdfAsync(org.Id);

        result.Should().NotBeNull();
        result!.Value.Content.Length.Should().BeGreaterThan(1000); // A real PDF, not a stub
        // PDF magic bytes: %PDF
        result.Value.Content.Take(4).Should().Equal((byte)'%', (byte)'P', (byte)'D', (byte)'F');
        result.Value.FileName.Should().Be("pdf-test-org-waiver-v1.pdf");
    }

    [Fact]
    public async Task GetCurrentWaiverPdfAsync_NoActiveWaiver_ReturnsNull()
    {
        var admin = await CreateTestUser();
        var org = await CreateTestOrganization(admin.Id);

        (await _sut.GetCurrentWaiverPdfAsync(org.Id)).Should().BeNull();

        // Cleared waiver also returns null
        await _sut.SetWaiverAsync(org.Id, "text", admin.Id);
        await _sut.SetWaiverAsync(org.Id, "", admin.Id);
        (await _sut.GetCurrentWaiverPdfAsync(org.Id)).Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentWaiverPdfAsync_WithBoldMarkers_StillRendersPdf()
    {
        var admin = await CreateTestUser();
        var org = await CreateTestOrganization(admin.Id);
        await _sut.SetWaiverAsync(org.Id, "**Section 1**\nBody text with **emphasis** and an unmatched ** marker.", admin.Id);

        var result = await _sut.GetCurrentWaiverPdfAsync(org.Id);

        result.Should().NotBeNull();
        result!.Value.Content.Length.Should().BeGreaterThan(1000);
        result.Value.Content.Take(4).Should().Equal((byte)'%', (byte)'P', (byte)'D', (byte)'F');
    }

    #endregion

    #region Bold Segment Parsing

    [Fact]
    public void ParseBoldSegments_NoMarkers_ReturnsSinglePlainSegment()
    {
        OrganizationWaiverService.ParseBoldSegments("Just a waiver.")
            .Should().Equal(("Just a waiver.", false));
    }

    [Fact]
    public void ParseBoldSegments_SinglePair_BoldsMarkedText()
    {
        OrganizationWaiverService.ParseBoldSegments("Read **carefully** please")
            .Should().Equal(("Read ", false), ("carefully", true), (" please", false));
    }

    [Fact]
    public void ParseBoldSegments_MultiplePairs_AlternateCorrectly()
    {
        OrganizationWaiverService.ParseBoldSegments("**A** and **B**")
            .Should().Equal(("A", true), (" and ", false), ("B", true));
    }

    [Fact]
    public void ParseBoldSegments_UnmatchedTrailingMarker_RendersLiterally()
    {
        OrganizationWaiverService.ParseBoldSegments("Signed **here and more text")
            .Should().Equal(("Signed ", false), ("**here and more text", false));
    }

    [Fact]
    public void ParseBoldSegments_BoldSpanningNewline_Preserved()
    {
        OrganizationWaiverService.ParseBoldSegments("**Section 1**\nBody")
            .Should().Equal(("Section 1", true), ("\nBody", false));
    }

    #endregion
}
