namespace BHMHockey.Api.Services;

/// <summary>Whether a user may see an event: the question the Results tab asks.</summary>
public interface IEventVisibility
{
    Task<bool> CanSeeAsync(Guid eventId, Guid? userId);
}

/// <summary>
/// Answers through EventService.GetByIdAsync, the one owner of the event
/// visibility rules (drafts, creator, org admins, Public / OrganizationMembers /
/// InviteOnly), so a result is never visible where its event is not. EventService
/// itself is unchanged.
/// </summary>
public class EventVisibility : IEventVisibility
{
    private readonly IEventService _eventService;

    public EventVisibility(IEventService eventService) => _eventService = eventService;

    public async Task<bool> CanSeeAsync(Guid eventId, Guid? userId) =>
        await _eventService.GetByIdAsync(eventId, userId) != null;
}
