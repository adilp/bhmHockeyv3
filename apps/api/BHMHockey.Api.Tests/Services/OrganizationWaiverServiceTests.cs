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
        await _sut.AcceptWaiverAsync(org.Id, v1!.Id, player.Id);
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

        await _sut.AcceptWaiverAsync(org.Id, waiver!.Id, player.Id);

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

        await _sut.AcceptWaiverAsync(org.Id, waiver!.Id, player.Id);
        await _sut.AcceptWaiverAsync(org.Id, waiver.Id, player.Id); // No throw

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

        var act = () => _sut.AcceptWaiverAsync(org.Id, v1!.Id, player.Id);

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

        var act = () => _sut.AcceptWaiverAsync(org.Id, v1!.Id, player.Id);

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

        var act = () => _sut.AcceptWaiverAsync(orgB.Id, waiverA!.Id, player.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
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
        await _sut.AcceptWaiverAsync(org.Id, waiver!.Id, player.Id);

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
        await _sut.AcceptWaiverAsync(org.Id, v1!.Id, player.Id);

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

    #endregion
}
