namespace BHMHockey.Api.Models.DTOs;

public record EventDto(
    Guid Id,
    Guid? OrganizationId,         // Nullable - standalone events have no org
    string? OrganizationName,     // Nullable - standalone events show null or creator name
    Guid CreatorId,               // Always present - who created the event
    string? Name,                 // Optional - null if no custom name set
    string? Description,
    DateTime EventDate,
    int Duration,
    string? Venue,
    int MaxPlayers,
    int RegisteredCount,
    decimal Cost,
    DateTime? RegistrationDeadline,
    string Status,
    string Visibility,            // Public, OrganizationMembers, InviteOnly
    List<string>? SkillLevels,    // Event's skill levels (overrides org if set)
    bool IsRegistered,
    bool CanManage,               // True if current user can manage this event (creator for standalone, org admin for org events)
    DateTime CreatedAt,
    // Payment fields (Phase 4)
    string? CreatorVenmoHandle,   // For "Pay with Venmo" button
    string? MyPaymentStatus,      // Current user's payment status (null if not registered or free event)
    // Team assignment
    string? MyTeamAssignment,     // Current user's team assignment ("Black" or "White")
    // Organizer fields (only populated when CanManage = true)
    int? UnpaidCount,             // Count of registrations with PaymentStatus != "Verified" (only for paid events)
    // Waitlist fields (Phase 5)
    int WaitlistCount,            // Number of people on waitlist
    int? MyWaitlistPosition,      // Current user's waitlist position (null if not waitlisted)
    DateTime? MyPaymentDeadline,  // Current user's payment deadline after promotion (null if none)
    bool AmIWaitlisted            // Convenience flag - true if current user is on waitlist
);

public record CreateEventRequest(
    DateTime EventDate,
    int MaxPlayers,
    decimal Cost,
    Guid? OrganizationId = null,         // Optional - omit for standalone pickup games
    string? Name = null,                 // Optional - generates default based on date if not provided
    string? Description = null,
    int? Duration = null,                // Optional - defaults to 60 minutes if not provided
    string? Venue = null,
    DateTime? RegistrationDeadline = null,
    string? Visibility = "Public",       // Default to public if not specified
    List<string>? SkillLevels = null     // Optional - overrides org's skill levels if set
);

public record UpdateEventRequest(
    string? Name,
    string? Description,
    DateTime? EventDate,
    int? Duration,
    string? Venue,
    int? MaxPlayers,
    decimal? Cost,
    DateTime? RegistrationDeadline,
    string? Status,
    string? Visibility,           // Can change visibility after creation
    List<string>? SkillLevels     // Can change skill levels after creation
);

public record EventRegistrationDto(
    Guid Id,
    Guid EventId,
    UserDto User,
    string Status,
    DateTime RegisteredAt,
    // Position tracking
    string? RegisteredPosition,  // "Goalie" or "Skater"
    // Payment fields (Phase 4)
    string? PaymentStatus,       // Pending, MarkedPaid, Verified, or null (free)
    DateTime? PaymentMarkedAt,
    DateTime? PaymentVerifiedAt,
    // Team assignment
    string? TeamAssignment,      // "Black" or "White"
    // Roster ordering
    int? RosterOrder,            // Order within team (lower = higher on roster)
    // Waitlist fields (Phase 5)
    int? WaitlistPosition,       // Position in waitlist (1 = first, null = not waitlisted)
    DateTime? PromotedAt,        // When user was promoted from waitlist
    DateTime? PaymentDeadlineAt, // Deadline to pay after promotion
    bool IsWaitlisted            // True if Status == "Waitlisted"
);

// Payment request DTOs (Phase 4)
public record MarkPaymentRequest(
    string? PaymentReference     // Optional: Venmo transaction ID or note
);

public record UpdatePaymentStatusRequest(
    string PaymentStatus         // "Verified" or "Pending" (to reset)
);

// Team assignment request DTO
public record UpdateTeamAssignmentRequest(
    string TeamAssignment        // "Black" or "White"
);

// Roster order update DTOs
public record RosterOrderItem(
    Guid RegistrationId,
    string TeamAssignment,       // "Black" or "White"
    int RosterOrder              // Order within team (0 = first)
);

public record UpdateRosterOrderRequest(
    List<RosterOrderItem> Items  // All registrations with their new order
);

// Registration request DTO
public record RegisterForEventRequest(
    string? Position             // "Goalie" or "Skater" (optional if user has only one position)
);

// Waitlist reorder DTOs (Phase 5)
public record WaitlistReorderItem(
    Guid RegistrationId,
    int Position
);

// Waitlist reorder request DTOs (for API endpoint)
public class ReorderWaitlistRequest
{
    public List<WaitlistOrderItem> Items { get; set; } = new();
}

public class WaitlistOrderItem
{
    public Guid RegistrationId { get; set; }
    public int Position { get; set; }
}

// Registration result DTO (Phase 5 - includes waitlist info)
public record RegistrationResultDto(
    string Status,               // "Registered" or "Waitlisted"
    int? WaitlistPosition,       // Position if waitlisted (null if registered)
    string Message               // User-friendly message
);

// Payment update result DTO (Phase 5 - payment verification response)
public record PaymentUpdateResultDto(
    bool Success,                // Whether the operation succeeded
    bool Promoted,               // True if user was promoted to roster, false if roster full or not applicable
    string Message,              // User-friendly message (e.g., "Payment verified - user promoted to roster")
    EventRegistrationDto? Registration  // Updated registration details (null on failure)
);
