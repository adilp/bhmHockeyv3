using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class EventService : IEventService
{
    private readonly AppDbContext _context;

    public EventService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EventDto> CreateAsync(CreateEventRequest request, Guid creatorId)
    {
        var evt = new Event
        {
            OrganizationId = request.OrganizationId,
            CreatorId = creatorId,
            Name = request.Name,
            Description = request.Description,
            EventDate = request.EventDate,
            Duration = request.Duration,
            Venue = request.Venue,
            MaxPlayers = request.MaxPlayers,
            Cost = request.Cost,
            RegistrationDeadline = request.RegistrationDeadline
        };

        _context.Events.Add(evt);
        await _context.SaveChangesAsync();

        return await MapToDto(evt, creatorId);
    }

    public async Task<List<EventDto>> GetAllAsync(Guid? currentUserId = null)
    {
        var events = await _context.Events
            .Include(e => e.Organization)
            .Include(e => e.Registrations)
            .Where(e => e.Status != "Cancelled" && e.EventDate >= DateTime.UtcNow)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        var dtos = new List<EventDto>();
        foreach (var evt in events)
        {
            dtos.Add(await MapToDto(evt, currentUserId));
        }

        return dtos;
    }

    public async Task<List<EventDto>> GetByOrganizationAsync(Guid organizationId, Guid? currentUserId = null)
    {
        var events = await _context.Events
            .Include(e => e.Organization)
            .Include(e => e.Registrations)
            .Where(e => e.OrganizationId == organizationId && e.Status != "Cancelled")
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        var dtos = new List<EventDto>();
        foreach (var evt in events)
        {
            dtos.Add(await MapToDto(evt, currentUserId));
        }

        return dtos;
    }

    public async Task<EventDto?> GetByIdAsync(Guid id, Guid? currentUserId = null)
    {
        var evt = await _context.Events
            .Include(e => e.Organization)
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);

        return evt == null ? null : await MapToDto(evt, currentUserId);
    }

    public async Task<EventDto?> UpdateAsync(Guid id, UpdateEventRequest request, Guid userId)
    {
        var evt = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == id && e.CreatorId == userId);

        if (evt == null) return null;

        if (request.Name != null) evt.Name = request.Name;
        if (request.Description != null) evt.Description = request.Description;
        if (request.EventDate.HasValue) evt.EventDate = request.EventDate.Value;
        if (request.Duration.HasValue) evt.Duration = request.Duration.Value;
        if (request.Venue != null) evt.Venue = request.Venue;
        if (request.MaxPlayers.HasValue) evt.MaxPlayers = request.MaxPlayers.Value;
        if (request.Cost.HasValue) evt.Cost = request.Cost.Value;
        if (request.RegistrationDeadline.HasValue) evt.RegistrationDeadline = request.RegistrationDeadline;
        if (request.Status != null) evt.Status = request.Status;

        evt.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await MapToDto(evt, userId);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        var evt = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == id && e.CreatorId == userId);

        if (evt == null) return false;

        evt.Status = "Cancelled";
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RegisterAsync(Guid eventId, Guid userId)
    {
        var evt = await _context.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null) return false;

        var existingReg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId);

        if (existingReg != null) return false;

        var registeredCount = evt.Registrations.Count(r => r.Status == "Registered");
        if (registeredCount >= evt.MaxPlayers)
        {
            throw new InvalidOperationException("Event is full");
        }

        var registration = new EventRegistration
        {
            EventId = eventId,
            UserId = userId
        };

        _context.EventRegistrations.Add(registration);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> CancelRegistrationAsync(Guid eventId, Guid userId)
    {
        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId);

        if (registration == null) return false;

        registration.Status = "Cancelled";
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<EventRegistrationDto>> GetRegistrationsAsync(Guid eventId)
    {
        var registrations = await _context.EventRegistrations
            .Include(r => r.User)
            .Where(r => r.EventId == eventId && r.Status == "Registered")
            .ToListAsync();

        return registrations.Select(r => new EventRegistrationDto(
            r.Id,
            r.EventId,
            new UserDto(
                r.User.Id,
                r.User.Email,
                r.User.FirstName,
                r.User.LastName,
                r.User.PhoneNumber,
                r.User.Role,
                r.User.CreatedAt
            ),
            r.Status,
            r.RegisteredAt
        )).ToList();
    }

    public async Task<List<EventDto>> GetUserRegistrationsAsync(Guid userId)
    {
        var registrations = await _context.EventRegistrations
            .Include(r => r.Event)
            .ThenInclude(e => e.Organization)
            .Include(r => r.Event.Registrations)
            .Where(r => r.UserId == userId && r.Status == "Registered")
            .Where(r => r.Event.EventDate >= DateTime.UtcNow)
            .OrderBy(r => r.Event.EventDate)
            .ToListAsync();

        var dtos = new List<EventDto>();
        foreach (var reg in registrations)
        {
            dtos.Add(await MapToDto(reg.Event, userId));
        }

        return dtos;
    }

    private async Task<EventDto> MapToDto(Event evt, Guid? currentUserId)
    {
        var registeredCount = evt.Registrations?.Count(r => r.Status == "Registered") ??
            await _context.EventRegistrations.CountAsync(r => r.EventId == evt.Id && r.Status == "Registered");

        var isRegistered = currentUserId.HasValue &&
            (evt.Registrations?.Any(r => r.UserId == currentUserId.Value && r.Status == "Registered") ??
             await _context.EventRegistrations.AnyAsync(r => r.EventId == evt.Id && r.UserId == currentUserId.Value && r.Status == "Registered"));

        var orgName = evt.Organization?.Name ??
            (await _context.Organizations.FindAsync(evt.OrganizationId))?.Name ?? "Unknown";

        return new EventDto(
            evt.Id,
            evt.OrganizationId,
            orgName,
            evt.Name,
            evt.Description,
            evt.EventDate,
            evt.Duration,
            evt.Venue,
            evt.MaxPlayers,
            registeredCount,
            evt.Cost,
            evt.RegistrationDeadline,
            evt.Status,
            isRegistered,
            evt.CreatedAt
        );
    }
}
