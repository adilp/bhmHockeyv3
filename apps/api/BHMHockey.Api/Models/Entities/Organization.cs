namespace BHMHockey.Api.Models.Entities;

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CreatorId { get; set; }
    public User Creator { get; set; } = null!;
    public string? Location { get; set; }
    public List<string>? SkillLevels { get; set; } // Gold, Silver, Bronze, D-League (multiple allowed)
    public bool IsActive { get; set; } = true;
    // Private orgs stay visible when browsing, but joining requires admin approval
    public bool IsPrivate { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Event Defaults (all optional)
    public int? DefaultDayOfWeek { get; set; }  // 0=Sunday, 6=Saturday
    public TimeSpan? DefaultStartTime { get; set; }  // Time of day
    public int? DefaultDurationMinutes { get; set; }
    public int? DefaultMaxPlayers { get; set; }
    public decimal? DefaultCost { get; set; }
    public string? DefaultVenue { get; set; }
    public string? DefaultVisibility { get; set; }  // "Public", "OrganizationMembers", "InviteOnly"
    public bool? DefaultShowWaitlistBeforePublish { get; set; }  // Pre-fills ShowWaitlistBeforePublish on new events
    // Overrides the global "new events start as drafts" default; null falls back to that global default
    public bool? DefaultStartAsDraft { get; set; }

    // Org-wide GroupMe chat link - events fall back to this unless they set their own
    public string? GroupMeLink { get; set; }

    // Navigation properties
    public ICollection<OrganizationSubscription> Subscriptions { get; set; } = new List<OrganizationSubscription>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<OrganizationAdmin> Admins { get; set; } = new List<OrganizationAdmin>();
}
