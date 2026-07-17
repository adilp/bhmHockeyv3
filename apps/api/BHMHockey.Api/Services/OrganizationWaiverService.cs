using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace BHMHockey.Api.Services;

public class OrganizationWaiverService : IOrganizationWaiverService
{
    private readonly AppDbContext _context;
    private readonly IOrganizationAdminService _adminService;
    private readonly ILogger<OrganizationWaiverService> _logger;

    public OrganizationWaiverService(
        AppDbContext context,
        IOrganizationAdminService adminService,
        ILogger<OrganizationWaiverService> logger)
    {
        _context = context;
        _adminService = adminService;
        _logger = logger;
    }

    /// <summary>
    /// Latest version row for the org, regardless of whether it is active.
    /// </summary>
    private async Task<OrganizationWaiver?> GetLatestWaiverAsync(Guid organizationId)
    {
        return await _context.OrganizationWaivers
            .Where(w => w.OrganizationId == organizationId)
            .OrderByDescending(w => w.Version)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Active waiver = latest version row IF its text is non-empty.
    /// </summary>
    private async Task<OrganizationWaiver?> GetActiveWaiverAsync(Guid organizationId)
    {
        var latest = await GetLatestWaiverAsync(organizationId);
        return latest != null && latest.Text.Length > 0 ? latest : null;
    }

    private static OrganizationWaiverDto MapToDto(OrganizationWaiver waiver)
    {
        return new OrganizationWaiverDto(
            waiver.Id,
            waiver.OrganizationId,
            waiver.Text,
            waiver.Version,
            waiver.CreatedAt
        );
    }

    public async Task<OrganizationWaiverDto?> GetCurrentWaiverAsync(Guid organizationId)
    {
        var active = await GetActiveWaiverAsync(organizationId);
        return active == null ? null : MapToDto(active);
    }

    public async Task<Guid?> GetActiveWaiverIdAsync(Guid organizationId)
    {
        var active = await GetActiveWaiverAsync(organizationId);
        return active?.Id;
    }

    public async Task<bool> IsAcceptanceRequiredAsync(Guid organizationId, Guid userId)
    {
        var activeId = await GetActiveWaiverIdAsync(organizationId);
        if (activeId == null)
        {
            return false;
        }

        var accepted = await _context.WaiverAcceptances
            .AnyAsync(a => a.WaiverId == activeId.Value && a.UserId == userId);
        return !accepted;
    }

    public async Task<OrganizationWaiverDto?> SetWaiverAsync(Guid organizationId, string? text, Guid userId)
    {
        var isAdmin = await _adminService.IsUserAdminAsync(organizationId, userId);
        if (!isAdmin)
        {
            _logger.LogWarning("Set waiver denied for organization {OrganizationId}: user {UserId} is not an admin", organizationId, userId);
            throw new UnauthorizedAccessException("Only organization admins can update the waiver");
        }

        var orgExists = await _context.Organizations.AnyAsync(o => o.Id == organizationId && o.IsActive);
        if (!orgExists)
        {
            _logger.LogWarning("Set waiver failed: organization {OrganizationId} not found", organizationId);
            throw new InvalidOperationException("Organization not found");
        }

        var trimmed = (text ?? string.Empty).Trim();
        var latest = await GetLatestWaiverAsync(organizationId);

        // No-op cases: clearing when nothing is active, or re-saving identical text
        // (avoids pointless version rows and accidental re-gating of all members)
        if (trimmed.Length == 0 && (latest == null || latest.Text.Length == 0))
        {
            return null;
        }
        if (latest != null && latest.Text == trimmed)
        {
            return MapToDto(latest);
        }

        // Immutable versioning: always a NEW row, never an update
        var waiver = new OrganizationWaiver
        {
            OrganizationId = organizationId,
            Text = trimmed,
            Version = (latest?.Version ?? 0) + 1,
            CreatedByUserId = userId
        };

        _context.OrganizationWaivers.Add(waiver);
        await _context.SaveChangesAsync();

        return trimmed.Length == 0 ? null : MapToDto(waiver);
    }

    public async Task AcceptWaiverAsync(Guid organizationId, Guid waiverId, Guid userId)
    {
        var active = await GetActiveWaiverAsync(organizationId);
        if (active == null || active.Id != waiverId)
        {
            _logger.LogWarning(
                "Waiver acceptance rejected for organization {OrganizationId}: waiver {WaiverId} is not the current active version",
                organizationId, waiverId);
            throw new InvalidOperationException("This waiver version is no longer current. Please review and accept the latest waiver.");
        }

        var alreadyAccepted = await _context.WaiverAcceptances
            .AnyAsync(a => a.WaiverId == waiverId && a.UserId == userId);
        if (alreadyAccepted)
        {
            return; // Idempotent
        }

        _context.WaiverAcceptances.Add(new WaiverAcceptance
        {
            UserId = userId,
            WaiverId = waiverId
        });
        await _context.SaveChangesAsync();
    }

    public async Task<List<PendingWaiverDto>> GetPendingWaiversAsync(Guid userId)
    {
        var now = DateTime.UtcNow;

        // Orgs where the user holds an active registration on an upcoming, non-cancelled event
        var orgIds = await _context.EventRegistrations
            .Where(r => r.UserId == userId
                && (r.Status == "Registered" || r.Status == "Waitlisted")
                && r.Event.OrganizationId != null
                && r.Event.EventDate > now
                && r.Event.Status != "Cancelled")
            .Select(r => r.Event.OrganizationId!.Value)
            .Distinct()
            .ToListAsync();

        if (orgIds.Count == 0)
        {
            return new List<PendingWaiverDto>();
        }

        var orgs = await _context.Organizations
            .Where(o => orgIds.Contains(o.Id) && o.IsActive)
            .OrderBy(o => o.Name)
            .ToListAsync();

        var pending = new List<PendingWaiverDto>();
        foreach (var org in orgs)
        {
            var active = await GetActiveWaiverAsync(org.Id);
            if (active == null)
            {
                continue;
            }

            var accepted = await _context.WaiverAcceptances
                .AnyAsync(a => a.WaiverId == active.Id && a.UserId == userId);
            if (!accepted)
            {
                pending.Add(new PendingWaiverDto(org.Id, org.Name, MapToDto(active)));
            }
        }

        return pending;
    }

    public async Task<(byte[] Content, string FileName)?> GetCurrentWaiverPdfAsync(Guid organizationId)
    {
        var active = await GetActiveWaiverAsync(organizationId);
        if (active == null)
        {
            return null;
        }

        var org = await _context.Organizations.FindAsync(organizationId);
        if (org == null || !org.IsActive)
        {
            return null;
        }

        var content = GenerateWaiverPdf(org.Name, active);
        var fileName = $"{SanitizeFileName(org.Name)}-waiver-v{active.Version}.pdf";
        return (content, fileName);
    }

    private static byte[] GenerateWaiverPdf(string organizationName, OrganizationWaiver waiver)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(54);
                page.DefaultTextStyle(style => style.FontSize(11));

                page.Header().Column(column =>
                {
                    column.Item().Text(organizationName).FontSize(20).SemiBold();
                    column.Item().Text($"Legal Waiver - Version {waiver.Version} - Effective {waiver.CreatedAt:MMMM d, yyyy}")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1);
                    column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                // Body text wraps and paginates automatically
                page.Content().PaddingVertical(14).Text(waiver.Text).LineHeight(1.4f);

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(9).FontColor(Colors.Grey.Darken1));
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string SanitizeFileName(string name)
    {
        var safe = new string(name
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-');
        while (safe.Contains("--"))
        {
            safe = safe.Replace("--", "-");
        }
        return string.IsNullOrEmpty(safe) ? "organization" : safe.ToLowerInvariant();
    }
}
