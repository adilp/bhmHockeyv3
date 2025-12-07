namespace BHMHockey.Api.Models.DTOs;

public record EventDto(
    Guid Id,
    Guid? OrganizationId,         // Nullable - standalone events have no org
    string? OrganizationName,     // Nullable - standalone events show null or creator name
    Guid CreatorId,               // Always present - who created the event
    string Name,
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
    // Organizer fields (only populated when CanManage = true)
    int? UnpaidCount              // Count of registrations with PaymentStatus != "Verified" (only for paid events)
);

public record CreateEventRequest(
    Guid? OrganizationId,         // Nullable - omit for standalone pickup games
    string Name,
    string? Description,
    DateTime EventDate,
    int Duration,
    string? Venue,
    int MaxPlayers,
    decimal Cost,
    DateTime? RegistrationDeadline,
    string? Visibility = "Public", // Default to public if not specified
    List<string>? SkillLevels = null  // Optional - overrides org's skill levels if set
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
    DateTime? PaymentVerifiedAt
);

// Payment request DTOs (Phase 4)
public record MarkPaymentRequest(
    string? PaymentReference     // Optional: Venmo transaction ID or note
);

public record UpdatePaymentStatusRequest(
    string PaymentStatus         // "Verified" or "Pending" (to reset)
);

// Registration request DTO
public record RegisterForEventRequest(
    string? Position             // "Goalie" or "Skater" (optional if user has only one position)
);
