# BHM Hockey - Complete Implementation Plan

Based on your updated PRD with its excellent simplicity philosophy and your existing monorepo structure, here's the full implementation plan with demoable phases.

## Project Overview

**Tech Stack Confirmation:**
- **Backend**: C# .NET 8, PostgreSQL, Docker Compose on Digital Ocean Droplet
- **Frontend**: React Native Expo (managed), TypeScript, Zustand
- **Real-time**: SignalR (in-app only) + Expo Push (background)
- **Payments**: Venmo deep linking (no direct API)
- **Deployment**: Single DO Droplet with Docker Compose

**Core Principles:**
- Keep it simple - no over-engineering
- Good enough for 100 users
- Monolith is fine
- Database as source of truth
- 15-minute background job intervals

---

## Phase 1: Foundation & Auth (Week 1)
**Goal**: Users can register, login, and set up their profile
**Demo**: Complete user onboarding flow

### Backend Implementation

#### 1.1 Update User Model
```csharp
// Models/Entities/User.cs
public class User
{
    public int Id { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    
    // New fields for hockey profile
    public string? SkillLevel { get; set; } // Gold, Silver, Bronze, D-League
    public string? Position { get; set; } // Forward, Defense, Goalie
    public string? VenmoHandle { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ExpoToken { get; set; } // For push notifications
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

#### 1.2 Create DTOs
```csharp
// Models/DTOs/UserProfileDto.cs
public class UserProfileDto
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string? SkillLevel { get; set; }
    public string? Position { get; set; }
    public string? VenmoHandle { get; set; }
    public string? PhoneNumber { get; set; }
}

// Models/DTOs/UpdateProfileDto.cs
public class UpdateProfileDto
{
    public string? SkillLevel { get; set; }
    public string? Position { get; set; }
    public string? VenmoHandle { get; set; }
    public string? PhoneNumber { get; set; }
}
```

#### 1.3 Update Controllers
```csharp
// Controllers/UsersController.cs
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        // Return current user profile
    }
    
    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult> UpdateProfile(UpdateProfileDto dto)
    {
        // Update user profile fields
    }
    
    [HttpPut("me/expo-token")]
    [Authorize]
    public async Task<ActionResult> UpdateExpoToken([FromBody] string token)
    {
        // Store Expo push token
    }
}
```

### Frontend Implementation

#### 1.4 Profile Screen
```typescript
// apps/mobile/app/(tabs)/profile.tsx
import { useState } from 'react';
import { View, ScrollView, TextInput, Picker } from 'react-native';
import { useUserStore } from '@/stores/userStore';

export default function ProfileScreen() {
  const { user, updateProfile } = useUserStore();
  const [skillLevel, setSkillLevel] = useState(user?.skillLevel || '');
  
  const SKILL_LEVELS = ['Gold', 'Silver', 'Bronze', 'D-League'];
  const POSITIONS = ['Forward', 'Defense', 'Goalie'];
  
  return (
    <ScrollView>
      <Picker selectedValue={skillLevel} onValueChange={setSkillLevel}>
        {SKILL_LEVELS.map(level => (
          <Picker.Item key={level} label={level} value={level} />
        ))}
      </Picker>
      
      <TextInput
        placeholder="Venmo Username"
        value={venmoHandle}
        onChangeText={setVenmoHandle}
      />
      
      <Button title="Save Profile" onPress={handleSave} />
    </ScrollView>
  );
}
```

### Database Migration
```bash
dotnet ef migrations add AddUserProfileFields
dotnet ef database update
```

### Demo Checklist
- [ ] User can register with email/password
- [ ] User can login
- [ ] User can set skill level and position
- [ ] User can add Venmo handle
- [ ] Expo push token is stored on login

---

## Phase 2: Organizations & Subscriptions (Week 2)
**Goal**: Create organizations and subscription system
**Demo**: Create org, browse orgs, subscribe for notifications

### Backend Implementation

#### 2.1 Create Models
```csharp
// Models/Entities/Organization.cs
public class Organization
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string? LogoUrl { get; set; }
    public int CreatorId { get; set; }
    public User Creator { get; set; }
    public string Location { get; set; }
    public string? SkillLevel { get; set; }
    public bool IsActive { get; set; }
    
    // Navigation properties
    public ICollection<OrganizationSubscription> Subscriptions { get; set; }
    public ICollection<Event> Events { get; set; }
}

// Models/Entities/OrganizationSubscription.cs
public class OrganizationSubscription
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int UserId { get; set; }
    public bool NotificationsEnabled { get; set; }
    public DateTime SubscribedAt { get; set; }
    
    public Organization Organization { get; set; }
    public User User { get; set; }
}
```

#### 2.2 Organization Controller
```csharp
// Controllers/OrganizationsController.cs
[ApiController]
[Route("api/[controller]")]
public class OrganizationsController : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<OrganizationDto>> Create(CreateOrganizationDto dto)
    {
        // Create organization, make creator an admin
    }
    
    [HttpGet]
    public async Task<ActionResult<List<OrganizationDto>>> GetAll()
    {
        // Return all active organizations
    }
    
    [HttpPost("{id}/subscribe")]
    [Authorize]
    public async Task<ActionResult> Subscribe(int id)
    {
        // Subscribe user to organization
        // Return subscription status
    }
    
    [HttpDelete("{id}/subscribe")]
    [Authorize]
    public async Task<ActionResult> Unsubscribe(int id)
    {
        // Remove subscription
    }
    
    [HttpGet("my-subscriptions")]
    [Authorize]
    public async Task<ActionResult<List<OrganizationDto>>> GetMySubscriptions()
    {
        // Return user's subscribed organizations
    }
}
```

### Frontend Implementation

#### 2.3 Organization Screens
```typescript
// apps/mobile/app/(tabs)/discover.tsx
export default function DiscoverScreen() {
  const [organizations, setOrganizations] = useState([]);
  
  useEffect(() => {
    fetchOrganizations();
  }, []);
  
  return (
    <FlatList
      data={organizations}
      renderItem={({ item }) => (
        <OrganizationCard
          org={item}
          onPress={() => navigateToOrg(item.id)}
        />
      )}
    />
  );
}

// apps/mobile/app/organizations/[id].tsx
export default function OrganizationDetailScreen() {
  const { id } = useLocalSearchParams();
  const [isSubscribed, setIsSubscribed] = useState(false);
  
  const handleSubscribe = async () => {
    await organizationService.subscribe(id);
    setIsSubscribed(true);
    // Request push notification permission if needed
    await registerForPushNotificationsAsync();
  };
  
  return (
    <View>
      <Button 
        title={isSubscribed ? "Unsubscribe" : "Subscribe"} 
        onPress={handleSubscribe}
      />
    </View>
  );
}
```

### Demo Checklist
- [ ] Create an organization
- [ ] Browse all organizations
- [ ] View organization details
- [ ] Subscribe to organization
- [ ] View "My Organizations" list
- [ ] Unsubscribe from organization

---

## Phase 3: Simple Events - Open Registration Only (Week 3)
**Goal**: Create and register for simple events (no waitlist/applications yet)
**Demo**: Organizer creates event, users can register

### Backend Implementation

#### 3.1 Event Models
```csharp
// Models/Entities/Event.cs
public class Event
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime EventDate { get; set; }
    public int Duration { get; set; } // minutes
    public string Venue { get; set; }
    public int MaxPlayers { get; set; }
    public decimal Cost { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public string Status { get; set; } // Draft, Published, Full, Completed, Cancelled
    
    public Organization Organization { get; set; }
    public ICollection<EventRegistration> Registrations { get; set; }
}

// Models/Entities/EventRegistration.cs (simple version)
public class EventRegistration
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int UserId { get; set; }
    public string Status { get; set; } // Registered, Cancelled
    public DateTime RegisteredAt { get; set; }
    
    public Event Event { get; set; }
    public User User { get; set; }
}
```

#### 3.2 Events Controller
```csharp
// Controllers/EventsController.cs
[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<EventDto>> Create(CreateEventDto dto)
    {
        // Verify user is organization admin
        // Create event
        // Send push notification to all subscribers
        await _notificationService.NotifyNewEvent(event);
    }
    
    [HttpGet("upcoming")]
    public async Task<ActionResult<List<EventDto>>> GetUpcoming()
    {
        // Return events from user's subscribed orgs
        // Include registration count
    }
    
    [HttpPost("{id}/register")]
    [Authorize]
    public async Task<ActionResult> Register(int id)
    {
        // Check if event is full
        // Create registration
        // Return success/full status
    }
    
    [HttpDelete("{id}/register")]
    [Authorize]
    public async Task<ActionResult> CancelRegistration(int id)
    {
        // Cancel registration
        // Don't worry about waitlist yet
    }
}
```

#### 3.3 Push Notification Service
```csharp
// Services/NotificationService.cs
public class NotificationService
{
    private readonly HttpClient _httpClient;
    
    public async Task NotifyNewEvent(Event evt)
    {
        var subscribers = await _db.OrganizationSubscriptions
            .Where(s => s.OrganizationId == evt.OrganizationId)
            .Include(s => s.User)
            .ToListAsync();
        
        var messages = subscribers
            .Where(s => !string.IsNullOrEmpty(s.User.ExpoToken))
            .Select(s => new
            {
                to = s.User.ExpoToken,
                title = $"New game from {evt.Organization.Name}",
                body = $"{evt.Name} - {evt.EventDate:MMM dd}",
                data = new { eventId = evt.Id }
            });
        
        // Send to Expo Push API
        await _httpClient.PostAsJsonAsync(
            "https://exp.host/--/api/v2/push/send",
            messages
        );
    }
}
```

### Frontend Implementation
```typescript
// apps/mobile/app/events/create.tsx
export default function CreateEventScreen() {
  const [formData, setFormData] = useState({
    name: '',
    eventDate: new Date(),
    venue: '',
    maxPlayers: 20,
    cost: 15,
  });
  
  const handleCreate = async () => {
    await eventService.create(formData);
    // Navigate to event details
  };
}

// apps/mobile/app/(tabs)/schedule.tsx
export default function ScheduleScreen() {
  const [events, setEvents] = useState([]);
  
  return (
    <FlatList
      data={events}
      renderItem={({ item }) => (
        <EventCard event={item}>
          <Text>{item.registrations.length}/{item.maxPlayers} players</Text>
          <Button title="Register" onPress={() => register(item.id)} />
        </EventCard>
      )}
    />
  );
}
```

### Demo Checklist
- [ ] Organizer creates event
- [ ] Subscribers receive push notification
- [ ] Users see events from subscribed orgs
- [ ] Users can register for events
- [ ] Registration count updates
- [ ] Users can cancel registration

---

## Phase 4: Venmo Integration & Payment Tracking (Week 4)
**Goal**: Add payment flow with Venmo deep linking
**Demo**: Register → Pay with Venmo → Mark as paid → Organizer verifies

### Backend Implementation

#### 4.1 Payment Models
```csharp
// Models/Entities/Payment.cs
public class Payment
{
    public int Id { get; set; }
    public int EventRegistrationId { get; set; }
    public decimal Amount { get; set; }
    public string RecipientVenmo { get; set; }
    public string PaymentNote { get; set; }
    public string Status { get; set; } // Pending, MarkedPaid, Verified
    public DateTime? MarkedPaidAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public int? VerifiedBy { get; set; }
    
    public EventRegistration Registration { get; set; }
}
```

#### 4.2 Payment Controller
```csharp
// Controllers/PaymentsController.cs
[HttpGet("events/{eventId}/payment-info")]
public async Task<ActionResult<PaymentInfoDto>> GetPaymentInfo(int eventId)
{
    // Return organizer's Venmo handle and amount
}

[HttpPost("{id}/mark-paid")]
[Authorize]
public async Task<ActionResult> MarkAsPaid(int id)
{
    // User marks payment as completed
    payment.Status = "MarkedPaid";
    payment.MarkedPaidAt = DateTime.UtcNow;
}

[HttpPost("{id}/verify")]
[Authorize]
public async Task<ActionResult> VerifyPayment(int id)
{
    // Organizer confirms payment received
    payment.Status = "Verified";
    payment.VerifiedAt = DateTime.UtcNow;
}

[HttpGet("events/{eventId}/payment-status")]
[Authorize]
public async Task<ActionResult<List<PaymentStatusDto>>> GetPaymentStatuses(int eventId)
{
    // Organizer view of all payment statuses
}
```

### Frontend Implementation
```typescript
// apps/mobile/services/PaymentService.ts
import * as Linking from 'expo-linking';

export class PaymentService {
  static async initiateVenmoPayment(
    recipientUsername: string,
    amount: number,
    note: string,
    paymentId: number
  ) {
    const venmoUrl = `venmo://paycharge?txn=pay&recipients=${recipientUsername}&amount=${amount}&note=${encodeURIComponent(note)}`;
    
    const canOpen = await Linking.canOpenURL(venmoUrl);
    if (canOpen) {
      await Linking.openURL(venmoUrl);
      // Show modal to confirm payment sent
      this.showPaymentConfirmationModal(paymentId);
    } else {
      // Show instructions to install Venmo
    }
  }
  
  static showPaymentConfirmationModal(paymentId: number) {
    Alert.alert(
      'Payment Sent?',
      'Did you complete the payment in Venmo?',
      [
        { text: 'No', style: 'cancel' },
        { 
          text: 'Yes', 
          onPress: () => this.markAsPaid(paymentId)
        }
      ]
    );
  }
}

// apps/mobile/app/events/[id]/payment.tsx
export default function PaymentScreen() {
  const handlePayWithVenmo = () => {
    PaymentService.initiateVenmoPayment(
      event.organizerVenmo,
      event.cost,
      `${event.name} - ${formatDate(event.eventDate)}`,
      registration.paymentId
    );
  };
  
  return (
    <View>
      <Text>Pay ${event.cost} to @{event.organizerVenmo}</Text>
      <Button title="Pay with Venmo" onPress={handlePayWithVenmo} />
      <Text>After paying in Venmo, return here to confirm</Text>
    </View>
  );
}
```

### Demo Checklist
- [ ] User registers for event
- [ ] User taps "Pay with Venmo"
- [ ] Venmo app opens with pre-filled info
- [ ] User returns and marks as paid
- [ ] Organizer sees payment as "MarkedPaid"
- [ ] Organizer verifies payment

---

## Phase 5: Waitlist & Auto-Promotion (Weeks 5-6)
**Goal**: Add waitlist with automatic promotion
**Demo**: Event fills up → Waitlist → Someone cancels → Auto-promotion with payment deadline

### Backend Implementation

#### 5.1 Enhanced Registration Model
```csharp
// Update EventRegistration.cs
public class EventRegistration
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int UserId { get; set; }
    public string Status { get; set; } // Registered, Waitlisted, Cancelled
    public int? WaitlistPosition { get; set; }
    public DateTime? PromotedAt { get; set; }
    public DateTime? PaymentDeadline { get; set; } // 2 hours after promotion
    public DateTime RegisteredAt { get; set; }
}
```

#### 5.2 Waitlist Service
```csharp
// Services/WaitlistService.cs
public class WaitlistService
{
    public async Task<RegistrationResult> RegisterForEvent(int eventId, int userId)
    {
        var registrationCount = await _db.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == "Registered");
        
        var registration = new EventRegistration
        {
            EventId = eventId,
            UserId = userId,
            RegisteredAt = DateTime.UtcNow
        };
        
        if (registrationCount >= evt.MaxPlayers)
        {
            registration.Status = "Waitlisted";
            registration.WaitlistPosition = await GetNextWaitlistPosition(eventId);
        }
        else
        {
            registration.Status = "Registered";
        }
        
        _db.EventRegistrations.Add(registration);
        await _db.SaveChangesAsync();
        
        return new RegistrationResult
        {
            Status = registration.Status,
            Position = registration.WaitlistPosition
        };
    }
    
    public async Task PromoteFromWaitlist(int eventId)
    {
        var nextInLine = await _db.EventRegistrations
            .Where(r => r.EventId == eventId && r.Status == "Waitlisted")
            .OrderBy(r => r.WaitlistPosition)
            .FirstOrDefaultAsync();
        
        if (nextInLine != null)
        {
            nextInLine.Status = "Registered";
            nextInLine.PromotedAt = DateTime.UtcNow;
            nextInLine.PaymentDeadline = DateTime.UtcNow.AddHours(2);
            
            // Send push notification
            await _notificationService.NotifyWaitlistPromotion(nextInLine);
            
            // Update other waitlist positions
            await UpdateWaitlistPositions(eventId);
        }
    }
}
```

#### 5.3 Background Service for Auto-Promotion
```csharp
// Services/Background/WaitlistPromotionService.cs
public class WaitlistPromotionService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckForPromotions();
            await CheckPaymentDeadlines();
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
    
    private async Task CheckPaymentDeadlines()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Find registrations past payment deadline
        var expiredRegistrations = await db.EventRegistrations
            .Where(r => r.Status == "Registered")
            .Where(r => r.PaymentDeadline != null)
            .Where(r => r.PaymentDeadline < DateTime.UtcNow)
            .Where(r => r.Payment.Status != "MarkedPaid")
            .ToListAsync();
        
        foreach (var reg in expiredRegistrations)
        {
            // Cancel registration
            reg.Status = "Cancelled";
            
            // Promote next person
            await _waitlistService.PromoteFromWaitlist(reg.EventId);
        }
        
        await db.SaveChangesAsync();
    }
}
```

### Frontend Implementation
```typescript
// apps/mobile/components/RegistrationStatus.tsx
export function RegistrationStatus({ registration, event }) {
  if (registration.status === 'Waitlisted') {
    return (
      <View style={styles.waitlistBadge}>
        <Text>Waitlist #{registration.waitlistPosition}</Text>
      </View>
    );
  }
  
  if (registration.paymentDeadline) {
    return (
      <CountdownTimer
        deadline={registration.paymentDeadline}
        onExpire={() => showExpiredAlert()}
      />
    );
  }
  
  return (
    <View style={styles.registeredBadge}>
      <Text>Registered ✓</Text>
    </View>
  );
}
```

### Demo Checklist
- [ ] Event reaches max capacity
- [ ] Next user joins waitlist
- [ ] Registered user cancels
- [ ] Waitlisted user receives promotion notification
- [ ] 2-hour payment timer starts
- [ ] If no payment, auto-skip to next
- [ ] Waitlist positions update

---

## Phase 6: Application System & Dynamic Opening (Week 7)
**Goal**: Add application-only events and automatic opening
**Demo**: Application required → Review/approve → Auto-open if low registration

### Backend Implementation

#### 6.1 Application Models
```csharp
// Update Event.cs
public class Event
{
    // ... existing fields
    public string RegistrationType { get; set; } // Open, ApplicationOnly, Hybrid
    public int? OpenRegistrationThreshold { get; set; } // e.g., 10 players
    public DateTime? OpenRegistrationTriggerTime { get; set; } // e.g., 24 hrs before
}

// Add EventApplication.cs
public class EventApplication
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int UserId { get; set; }
    public string ApplicationText { get; set; }
    public string Status { get; set; } // Pending, Approved, Rejected
    public DateTime AppliedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedBy { get; set; }
    public string? RejectionReason { get; set; }
}
```

#### 6.2 Application Controller
```csharp
// Controllers/EventApplicationsController.cs
[HttpPost("events/{eventId}/apply")]
[Authorize]
public async Task<ActionResult> Apply(int eventId, ApplyDto dto)
{
    var application = new EventApplication
    {
        EventId = eventId,
        UserId = GetUserId(),
        ApplicationText = dto.ApplicationText,
        Status = "Pending",
        AppliedAt = DateTime.UtcNow
    };
    
    _db.EventApplications.Add(application);
    await _db.SaveChangesAsync();
    
    // Notify organizer
    await _notificationService.NotifyNewApplication(application);
}

[HttpPost("applications/{id}/approve")]
[Authorize]
public async Task<ActionResult> Approve(int id)
{
    var application = await _db.EventApplications.FindAsync(id);
    application.Status = "Approved";
    application.ReviewedAt = DateTime.UtcNow;
    
    // Create registration
    await _registrationService.RegisterFromApplication(application);
    
    // Notify applicant
    await _notificationService.NotifyApplicationApproved(application);
}

[HttpPost("applications/{id}/reject")]
[Authorize]
public async Task<ActionResult> Reject(int id, RejectDto dto)
{
    var application = await _db.EventApplications.FindAsync(id);
    application.Status = "Rejected";
    application.RejectionReason = dto.Reason;
    
    // Notify applicant
    await _notificationService.NotifyApplicationRejected(application);
}
```

#### 6.3 Dynamic Opening Service
```csharp
// Services/Background/DynamicOpeningService.cs
public class DynamicOpeningService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckForOpeningTriggers();
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
    
    private async Task CheckForOpeningTriggers()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var eventsToOpen = await db.Events
            .Where(e => e.RegistrationType == "Hybrid")
            .Where(e => e.OpenRegistrationTriggerTime != null)
            .Where(e => e.OpenRegistrationTriggerTime < DateTime.UtcNow)
            .Where(e => e.Status == "Published")
            .Include(e => e.Registrations)
            .ToListAsync();
        
        foreach (var evt in eventsToOpen)
        {
            var registrationCount = evt.Registrations.Count(r => r.Status == "Registered");
            
            if (registrationCount < evt.OpenRegistrationThreshold)
            {
                evt.RegistrationType = "Open";
                
                // Notify all non-registered users
                await _notificationService.NotifyEventOpenedUp(evt);
            }
        }
        
        await db.SaveChangesAsync();
    }
}
```

### Frontend Implementation
```typescript
// apps/mobile/app/events/[id]/apply.tsx
export default function ApplyScreen() {
  const [applicationText, setApplicationText] = useState('');
  
  const handleSubmit = async () => {
    await eventService.apply(eventId, { applicationText });
    Alert.alert('Applied!', 'The organizer will review your application.');
  };
  
  return (
    <View>
      <Text>Tell us about your experience:</Text>
      <TextInput
        multiline
        numberOfLines={4}
        value={applicationText}
        onChangeText={setApplicationText}
        placeholder="I've been playing for 5 years..."
      />
      <Button title="Submit Application" onPress={handleSubmit} />
    </View>
  );
}

// apps/mobile/app/events/[id]/applications.tsx (Organizer view)
export default function ApplicationsScreen() {
  const [applications, setApplications] = useState([]);
  
  return (
    <FlatList
      data={applications}
      renderItem={({ item }) => (
        <ApplicationCard application={item}>
          <Button title="Approve" onPress={() => approve(item.id)} />
          <Button title="Reject" onPress={() => reject(item.id)} />
        </ApplicationCard>
      )}
    />
  );
}
```

### Demo Checklist
- [ ] Create application-only event
- [ ] User submits application
- [ ] Organizer reviews applications
- [ ] Approve/reject with notification
- [ ] Set hybrid event with threshold
- [ ] Event auto-opens when threshold not met
- [ ] Non-members receive "now open" notification

---

## Phase 7: Real-time Updates with SignalR (Week 8)
**Goal**: Add live in-app updates for better UX
**Demo**: Live registration counts, instant cancellation alerts

### Backend Implementation

#### 7.1 SignalR Hub
```csharp
// Hubs/EventHub.cs
public class EventHub : Hub
{
    public async Task JoinEventGroup(int eventId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"event-{eventId}");
        
        // Send current registration count
        var count = await _db.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == "Registered");
        
        await Clients.Caller.SendAsync("RegistrationCount", count);
    }
    
    public async Task LeaveEventGroup(int eventId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"event-{eventId}");
    }
}

// Update EventsController.cs to broadcast changes
[HttpPost("{id}/register")]
public async Task<ActionResult> Register(int id)
{
    // ... existing registration logic
    
    // Broadcast new count to all watching
    await _hubContext.Clients.Group($"event-{id}")
        .SendAsync("RegistrationUpdate", new { 
            count = newCount,
            userId = GetUserId(),
            action = "registered"
        });
}
```

#### 7.2 Configure SignalR
```csharp
// Program.cs
builder.Services.AddSignalR();

app.MapHub<EventHub>("/hubs/events");
```

### Frontend Implementation
```typescript
// apps/mobile/services/SignalRService.ts
import * as signalR from '@microsoft/signalr';

export class SignalRService {
  private connection: signalR.HubConnection;
  
  async connect() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/events`)
      .withAutomaticReconnect()
      .build();
    
    await this.connection.start();
  }
  
  async joinEvent(eventId: number, onUpdate: (data: any) => void) {
    await this.connection.invoke('JoinEventGroup', eventId);
    
    this.connection.on('RegistrationUpdate', onUpdate);
    this.connection.on('RegistrationCount', onUpdate);
  }
  
  async leaveEvent(eventId: number) {
    await this.connection.invoke('LeaveEventGroup', eventId);
    this.connection.off('RegistrationUpdate');
  }
}

// apps/mobile/app/events/[id]/index.tsx
export default function EventDetailScreen() {
  const [registrationCount, setRegistrationCount] = useState(0);
  const signalR = useSignalR();
  
  useEffect(() => {
    signalR.joinEvent(eventId, (data) => {
      setRegistrationCount(data.count);
      if (data.action === 'cancelled') {
        Alert.alert('Spot opened!', 'Someone just cancelled');
      }
    });
    
    return () => {
      signalR.leaveEvent(eventId);
    };
  }, [eventId]);
  
  return (
    <View>
      <Text>{registrationCount}/{event.maxPlayers} registered</Text>
      {/* Live updating! */}
    </View>
  );
}
```

### Demo Checklist
- [ ] Open event on two devices
- [ ] Register on device A
- [ ] Count updates instantly on device B
- [ ] Cancel on device A
- [ ] "Spot opened" alert on device B
- [ ] Connection auto-reconnects if dropped

---

## Phase 8: Payment Reminders & Notification Batching (Week 9)
**Goal**: Automated payment reminders and smart notification batching
**Demo**: Automatic payment reminders, batched notifications

### Backend Implementation

#### 8.1 Payment Reminder Service
```csharp
// Services/Background/PaymentReminderService.cs
public class PaymentReminderService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await SendPaymentReminders();
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
    
    private async Task SendPaymentReminders()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // 48-hour reminder
        var reminder48Hr = await db.EventRegistrations
            .Where(r => r.Status == "Registered")
            .Where(r => r.Payment.Status == "Pending")
            .Where(r => r.Event.EventDate > DateTime.UtcNow.AddHours(47))
            .Where(r => r.Event.EventDate < DateTime.UtcNow.AddHours(49))
            .Where(r => !r.RemindersSent.Contains("48hr"))
            .Include(r => r.User)
            .Include(r => r.Event)
            .ToListAsync();
        
        var notifications = new List<ExpoPushMessage>();
        
        foreach (var reg in reminder48Hr)
        {
            notifications.Add(new ExpoPushMessage
            {
                To = reg.User.ExpoToken,
                Title = "Payment Reminder",
                Body = $"Payment due for {reg.Event.Name} (48 hours)",
                Data = new { eventId = reg.EventId, type = "payment_reminder" }
            });
            
            reg.RemindersSent = (reg.RemindersSent ?? "") + ",48hr";
        }
        
        if (notifications.Any())
        {
            await _expoClient.SendPushNotificationsAsync(notifications);
            await db.SaveChangesAsync();
        }
    }
}
```

#### 8.2 Notification Batching Service
```csharp
// Services/Background/NotificationBatchService.cs
public class NotificationBatchService : BackgroundService
{
    private readonly Queue<PendingNotification> _notificationQueue = new();
    
    public void QueueNotification(PendingNotification notification)
    {
        _notificationQueue.Enqueue(notification);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatch();
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
    
    private async Task ProcessBatch()
    {
        var grouped = _notificationQueue
            .GroupBy(n => new { n.UserId, n.Type })
            .Select(g => new
            {
                UserId = g.Key.UserId,
                Type = g.Key.Type,
                Count = g.Count(),
                Items = g.ToList()
            });
        
        var messages = new List<ExpoPushMessage>();
        
        foreach (var group in grouped)
        {
            var user = await _db.Users.FindAsync(group.UserId);
            
            if (group.Count > 1)
            {
                // Batch multiple notifications
                messages.Add(new ExpoPushMessage
                {
                    To = user.ExpoToken,
                    Title = GetBatchTitle(group.Type, group.Count),
                    Body = GetBatchBody(group.Items),
                    Badge = group.Count
                });
            }
            else
            {
                // Single notification
                var item = group.Items.First();
                messages.Add(new ExpoPushMessage
                {
                    To = user.ExpoToken,
                    Title = item.Title,
                    Body = item.Body,
                    Data = item.Data
                });
            }
        }
        
        await _expoClient.SendPushNotificationsAsync(messages);
        _notificationQueue.Clear();
    }
}
```

### Demo Checklist
- [ ] Set up event 48 hours out
- [ ] Register without paying
- [ ] Receive 48-hour reminder
- [ ] Receive 24-hour reminder
- [ ] Multiple events batch into single notification
- [ ] Notification preferences work

---

## Phase 9: Polish, Testing & Optimization (Week 10)
**Goal**: Final polish, error handling, performance optimization
**Demo**: Complete user journey with all features

### Key Tasks

#### 9.1 Error Handling
```typescript
// apps/mobile/services/ErrorHandler.ts
export class ErrorHandler {
  static handle(error: any, context: string) {
    if (error.response?.status === 401) {
      // Auto logout
      authService.logout();
      NavigationService.navigate('Login');
    } else if (error.response?.status === 400) {
      Alert.alert('Error', error.response.data.message);
    } else if (!error.response) {
      Alert.alert('Connection Error', 'Please check your internet connection');
    } else {
      Alert.alert('Something went wrong', 'Please try again');
    }
  }
}
```

#### 9.2 Loading States
```typescript
// apps/mobile/components/LoadingOverlay.tsx
export function LoadingOverlay({ visible, message }) {
  if (!visible) return null;
  
  return (
    <View style={styles.overlay}>
      <ActivityIndicator size="large" />
      <Text>{message || 'Loading...'}</Text>
    </View>
  );
}
```

#### 9.3 Offline Queue
```typescript
// apps/mobile/services/OfflineQueue.ts
export class OfflineQueue {
  private queue: QueueItem[] = [];
  
  async add(action: () => Promise<void>) {
    if (await NetInfo.fetch().then(state => state.isConnected)) {
      return action();
    } else {
      this.queue.push({ action, timestamp: Date.now() });
      await AsyncStorage.setItem('offlineQueue', JSON.stringify(this.queue));
    }
  }
  
  async processQueue() {
    const isConnected = await NetInfo.fetch().then(state => state.isConnected);
    if (!isConnected) return;
    
    for (const item of this.queue) {
      try {
        await item.action();
      } catch (error) {
        console.error('Failed to process queued item', error);
      }
    }
    
    this.queue = [];
    await AsyncStorage.removeItem('offlineQueue');
  }
}
```

### Testing Checklist
- [ ] Complete registration flow
- [ ] Organization subscription flow
- [ ] Event creation and registration
- [ ] Payment flow with Venmo
- [ ] Waitlist promotion
- [ ] Application approval
- [ ] Push notifications work
- [ ] SignalR reconnection
- [ ] Offline handling
- [ ] Error states

---

## Phase 10: Docker Deployment to Digital Ocean (Week 11)
**Goal**: Deploy to production on Digital Ocean Droplet
**Demo**: Live production app

### 10.1 Docker Configuration
```yaml
# docker-compose.yml
version: '3.8'

services:
  db:
    image: postgres:15
    container_name: bhmhockey-db
    environment:
      POSTGRES_DB: bhmhockey
      POSTGRES_USER: bhmhockey
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    restart: unless-stopped
    networks:
      - bhmhockey-network

  api:
    build: 
      context: ./apps/api
      dockerfile: Dockerfile
    container_name: bhmhockey-api
    environment:
      ConnectionStrings__DefaultConnection: Host=db;Database=bhmhockey;Username=bhmhockey;Password=${DB_PASSWORD}
      Jwt__Secret: ${JWT_SECRET}
      Expo__AccessToken: ${EXPO_ACCESS_TOKEN}
      ASPNETCORE_ENVIRONMENT: Production
    ports:
      - "5000:80"
    depends_on:
      - db
    restart: unless-stopped
    networks:
      - bhmhockey-network

  nginx:
    image: nginx:alpine
    container_name: bhmhockey-nginx
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf
      - ./ssl:/etc/nginx/ssl
      - ./certbot/www:/var/www/certbot
    depends_on:
      - api
    restart: unless-stopped
    networks:
      - bhmhockey-network

volumes:
  postgres_data:

networks:
  bhmhockey-network:
    driver: bridge
```

### 10.2 Deployment Script
```bash
#!/bin/bash
# deploy.sh

# SSH into Digital Ocean droplet
ssh root@your-droplet-ip << 'ENDSSH'

# Pull latest code
cd /opt/bhmhockey
git pull origin main

# Build and restart containers
docker-compose down
docker-compose build --no-cache
docker-compose up -d

# Run migrations
docker exec bhmhockey-api dotnet ef database update

# Check health
curl http://localhost:5000/health

ENDSSH
```

### 10.3 SSL Setup with Let's Encrypt
```bash
# On the droplet
sudo apt-get update
sudo apt-get install certbot

# Get SSL certificate
sudo certbot certonly --webroot \
  -w /var/www/certbot \
  -d yourdomain.com \
  -d api.yourdomain.com

# Auto-renewal
sudo crontab -e
# Add: 0 0 * * * certbot renew --quiet
```

### Deployment Checklist
- [ ] Create Digital Ocean droplet (4GB RAM, 2 CPU)
- [ ] Install Docker and Docker Compose
- [ ] Clone repository
- [ ] Set environment variables
- [ ] Run docker-compose up
- [ ] Configure domain and SSL
- [ ] Test all endpoints
- [ ] Set up monitoring
- [ ] Configure backups

---

## Phase 11: Mobile App Release (Week 12)
**Goal**: Submit to App Store and Google Play
**Demo**: App available in stores

### 11.1 iOS Release
```bash
cd apps/mobile

# Configure app.json
eas build:configure

# Build for App Store
eas build --platform ios --profile production

# Submit to App Store
eas submit --platform ios
```

### 11.2 Android Release
```bash
# Build for Google Play
eas build --platform android --profile production

# Submit to Google Play
eas submit --platform android
```

### Release Checklist
- [ ] App icons and splash screens
- [ ] App Store screenshots
- [ ] Privacy policy
- [ ] Terms of service
- [ ] App Store description
- [ ] Test on multiple devices
- [ ] Submit for review
- [ ] Monitor crash reports

---

## Future Phases (Post-Launch)

### Phase 12: Tournament System (Month 4-5)
- Tournament creation
- Team management
- Bracket generation
- Match scheduling
- Results tracking

### Phase 13: Team Chat (Month 5-6)
- Team group chats
- File sharing
- Polls
- Read receipts

### Phase 14: League Substitute Finder (Month 6)
- Post sub requests
- Sub availability
- Quick fill
- Ratings

---

## Success Metrics

### Technical Metrics
- [ ] < 3 second load times
- [ ] < 1% crash rate
- [ ] 99% uptime
- [ ] < 500ms API response time

### User Metrics
- [ ] 50+ active users in 3 months
- [ ] 5+ active organizations
- [ ] 80% event fill rate
- [ ] < 10% no-show rate

### Engagement Metrics
- [ ] 60% weekly active users
- [ ] 75% subscription rate to 1+ org
- [ ] 95% notification delivery rate
- [ ] < 5% uninstall rate

---

## Key Development Commands

```bash
# Development
yarn dev                     # Run everything locally
yarn api                     # Run API only
yarn mobile                  # Run mobile only

# Database
yarn api:migrations AddName  # Create migration
yarn api:update-db          # Apply migrations

# Testing
yarn test                    # Run tests
yarn test:integration       # Integration tests

# Deployment
./deploy.sh                 # Deploy to Digital Ocean
eas build --platform all    # Build mobile apps
eas submit --platform all   # Submit to stores

# Monitoring
docker logs bhmhockey-api   # View API logs
docker stats                # Monitor resources
```

---

This complete implementation plan gives you:
1. **Clear weekly goals** with demoable features
2. **Practical code examples** that follow your simplicity philosophy
3. **No over-engineering** - just what works for 100 users
4. **Incremental complexity** - start simple, add features gradually
5. **Production-ready path** from local development to app stores

The plan avoids all the complexity traps you identified (no microservices, no Redis, no Kubernetes, etc.) while delivering a fully functional app that solves real problems for your hockey community.
