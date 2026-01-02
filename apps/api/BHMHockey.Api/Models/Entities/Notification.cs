using System.ComponentModel.DataAnnotations.Schema;

namespace BHMHockey.Api.Models.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // User who receives this notification
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Notification content
    public string Type { get; set; } = string.Empty;  // "new_event", "waitlist_promoted", etc.
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    // JSON data for deep linking (eventId, orgId, etc.)
    public string? Data { get; set; }

    // Optional references for future muting support
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public Guid? EventId { get; set; }
    public Event? Event { get; set; }

    // Read status - null means unread
    public DateTime? ReadAt { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Computed property
    [NotMapped]
    public bool IsRead => ReadAt.HasValue;
}
