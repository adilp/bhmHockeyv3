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

    public async Task AcceptWaiverAsync(Guid organizationId, AcceptWaiverRequest request, Guid userId)
    {
        var active = await GetActiveWaiverAsync(organizationId);
        if (active == null || active.Id != request.WaiverId)
        {
            _logger.LogWarning(
                "Waiver acceptance rejected for organization {OrganizationId}: waiver {WaiverId} is not the current active version",
                organizationId, request.WaiverId);
            throw new InvalidOperationException("This waiver version is no longer current. Please review and accept the latest waiver.");
        }

        var signature = ValidateSignatureFields(organizationId, request);

        // The adult printed name is the account holder's attestation - it must
        // match their profile name (guardian/minor names are other people and
        // are not checked)
        var user = await _context.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found");
        var profileName = $"{user.FirstName} {user.LastName}";
        if (!NamesMatch(signature.ParticipantName, profileName))
        {
            _logger.LogWarning(
                "Waiver acceptance rejected for user {UserId}: printed name does not match profile name",
                userId);
            throw new InvalidOperationException(
                $"Printed name must match the name on your profile: {profileName}");
        }

        var alreadyAccepted = await _context.WaiverAcceptances
            .AnyAsync(a => a.WaiverId == request.WaiverId && a.UserId == userId);
        if (alreadyAccepted)
        {
            // Idempotent - the original acceptance row (including its signature
            // fields) is an immutable audit record and is never overwritten
            return;
        }

        _context.WaiverAcceptances.Add(new WaiverAcceptance
        {
            UserId = userId,
            WaiverId = request.WaiverId,
            ParticipantName = signature.ParticipantName,
            ParticipantDate = signature.ParticipantDate,
            MinorParticipantName = signature.MinorParticipantName,
            MinorDateOfBirth = signature.MinorDateOfBirth,
            GuardianName = signature.GuardianName,
            GuardianSignature = signature.GuardianSignature,
            GuardianDate = signature.GuardianDate
        });
        await _context.SaveChangesAsync();
    }

    // Max stored length for each signature text field (also enforced by EF config)
    private const int SignatureFieldMaxLength = 200;

    /// <summary>
    /// Case-insensitive, whitespace-normalized name comparison: "jane  skater"
    /// matches "Jane Skater"; initials or different names do not. Mirrored
    /// client-side in apps/mobile/utils/waiverSignature.ts - keep in sync.
    /// </summary>
    private static bool NamesMatch(string entered, string profileName)
    {
        static string Normalize(string value) => string.Join(' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        return Normalize(entered) == Normalize(profileName);
    }

    private record ValidatedSignature(
        string ParticipantName,
        DateTime ParticipantDate,
        string? MinorParticipantName,
        DateTime? MinorDateOfBirth,
        string? GuardianName,
        string? GuardianSignature,
        DateTime? GuardianDate
    );

    /// <summary>
    /// Server-side mirror of the acceptance form rules: participant name/date
    /// required; Parent/Guardian section all-or-nothing with the minor's date
    /// of birth in the past. Strings are trimmed and length-capped; dates are
    /// normalized to calendar dates at UTC midnight.
    /// </summary>
    private ValidatedSignature ValidateSignatureFields(Guid organizationId, AcceptWaiverRequest request)
    {
        void Reject(string message)
        {
            _logger.LogWarning(
                "Waiver acceptance rejected for organization {OrganizationId}: {Message}",
                organizationId, message);
            throw new InvalidOperationException(message);
        }

        string? Clean(string? value, string label)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }
            if (trimmed.Length > SignatureFieldMaxLength)
            {
                Reject($"{label} must be {SignatureFieldMaxLength} characters or fewer.");
            }
            return trimmed;
        }

        var participantName = Clean(request.ParticipantName, "Printed name");
        if (participantName == null)
        {
            Reject("Printed name is required.");
        }

        var minorName = Clean(request.MinorParticipantName, "Minor participant's printed name");
        var guardianName = Clean(request.GuardianName, "Parent/guardian printed name");
        var guardianSignature = Clean(request.GuardianSignature, "Parent/guardian signature");

        var anyMinorField = minorName != null || request.MinorDateOfBirth != null
            || guardianName != null || guardianSignature != null;

        if (anyMinorField)
        {
            var allMinorFields = minorName != null && request.MinorDateOfBirth != null
                && guardianName != null && guardianSignature != null;
            if (!allMinorFields)
            {
                Reject("The Parent/Guardian section is all-or-nothing: provide the minor's name, date of birth, parent/guardian name, and signature - or leave the whole section empty.");
            }
            if (AsUtcDate(request.MinorDateOfBirth!.Value) >= DateTime.UtcNow.Date)
            {
                Reject("Minor's date of birth must be in the past.");
            }
        }

        // Signature dates are stamped by the server, never taken from the
        // client - the recorded date is always the actual acceptance date
        var signedOn = DateTime.UtcNow.Date;
        return new ValidatedSignature(
            participantName!,
            signedOn,
            anyMinorField ? minorName : null,
            anyMinorField ? AsUtcDate(request.MinorDateOfBirth!.Value) : null,
            anyMinorField ? guardianName : null,
            anyMinorField ? guardianSignature : null,
            anyMinorField ? signedOn : null
        );
    }

    /// <summary>
    /// Signature dates are calendar dates (the client sends YYYY-MM-DD, which
    /// binds with Kind=Unspecified). Store them at UTC midnight per the repo's
    /// all-dates-are-UTC convention (Npgsql rejects non-UTC kinds).
    /// </summary>
    private static DateTime AsUtcDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    public async Task<List<PendingWaiverDto>> GetPendingWaiversAsync(Guid userId)
    {
        var now = DateTime.UtcNow;

        // Every org the user is a MEMBER of must be current on the waiver...
        var subscribedOrgIds = await _context.OrganizationSubscriptions
            .Where(s => s.UserId == userId)
            .Select(s => s.OrganizationId)
            .ToListAsync();

        // ...plus orgs where a non-member holds an active registration on an
        // upcoming, non-cancelled event (e.g. manually added by an organizer)
        var registeredOrgIds = await _context.EventRegistrations
            .Where(r => r.UserId == userId
                && (r.Status == "Registered" || r.Status == "Waitlisted")
                && r.Event.OrganizationId != null
                && r.Event.EventDate > now
                && r.Event.Status != "Cancelled")
            .Select(r => r.Event.OrganizationId!.Value)
            .ToListAsync();

        var orgIds = subscribedOrgIds.Union(registeredOrgIds).ToList();

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

                // Body text wraps and paginates automatically; **markers** render bold
                page.Content().PaddingVertical(14).Text(text =>
                {
                    text.DefaultTextStyle(style => style.LineHeight(1.4f));
                    foreach (var (segment, bold) in ParseBoldSegments(waiver.Text))
                    {
                        if (bold)
                        {
                            text.Span(segment).SemiBold();
                        }
                        else
                        {
                            text.Span(segment);
                        }
                    }
                });

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

    /// <summary>
    /// Splits waiver text on ** bold markers. Mirrors the mobile parser
    /// (apps/mobile/utils/waiverFormat.ts) — keep the two in sync. An
    /// unmatched trailing ** is emitted literally rather than bolding the
    /// remainder of the document.
    /// </summary>
    public static IReadOnlyList<(string Text, bool Bold)> ParseBoldSegments(string text)
    {
        var parts = text.Split("**");
        if (parts.Length == 1)
        {
            return new[] { (text, false) };
        }

        var unbalanced = parts.Length % 2 == 0;
        var segments = new List<(string, bool)>();
        for (var i = 0; i < parts.Length; i++)
        {
            var isLast = i == parts.Length - 1;
            if (unbalanced && isLast)
            {
                segments.Add(($"**{parts[i]}", false));
            }
            else if (parts[i].Length > 0)
            {
                segments.Add((parts[i], i % 2 == 1));
            }
        }
        return segments;
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
