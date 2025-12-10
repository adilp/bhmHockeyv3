namespace BHMHockey.Api.Models.Entities;

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Organization is optional - null means standalone/pickup game
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid CreatorId { get; set; }
    public User Creator { get; set; } = null!;
    public string? Name { get; set; }  // Optional - null means no custom name
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public int Duration { get; set; } = 60; // minutes
    public string? Venue { get; set; }
    public int MaxPlayers { get; set; } = 20;
    public decimal Cost { get; set; } = 0;
    public DateTime? RegistrationDeadline { get; set; }
    public string Status { get; set; } = "Published"; // Draft, Published, Full, Completed, Cancelled

    // Visibility controls who can see and register for the event
    // - Public: Anyone can see and register
    // - OrganizationMembers: Only subscribers of the organization (requires OrganizationId)
    // - InviteOnly: Only invited users can see/register (Phase B: will use EventInvitation table)
    public string Visibility { get; set; } = "Public";

    // Optional skill levels - if set, overrides organization's skill levels
    public List<string>? SkillLevels { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Reminder tracking - to avoid sending duplicates
    public DateTime? PlayerReminderSentAt { get; set; }
    public DateTime? OrganizerPaymentReminderSentAt { get; set; }

    // Navigation properties
    public ICollection<EventRegistration> Registrations { get; set; } = new List<EventRegistration>();

    // Phase B: Uncomment when implementing invite system
    // public ICollection<EventInvitation> Invitations { get; set; } = new List<EventInvitation>();
}
