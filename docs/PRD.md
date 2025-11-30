Excellent! Based on your answers, let me create a comprehensive PRD for your hockey pickup game management app. I'll focus on the MVP (pickup groups) with considerations for future features.

# Product Requirements Document
## Hockey Pickup Game Management App

### 1. Executive Summary
A community-focused mobile application for organizing, managing, and participating in hockey pickup games at a local rink. The app streamlines the current manual process of organizing games via messaging and Venmo payments, providing a centralized platform for game management, team balancing, and payment coordination.

**Current Status (2025-11-30):** MVP Complete ✅
- User authentication & profiles
- Organizations with multi-admin support
- Events with registration
- Venmo payment coordination
- Push notifications

### 2. Product Overview

**Vision:** Create a frictionless experience for hockey players to find, join, and pay for pickup games while giving organizers powerful tools to manage their groups and games.

**MVP Scope:** Pickup game group management with registration, waitlist, team balancing, and Venmo payment coordination.

**Future Scope:** Tournament management, league substitute finder, and enhanced communication features.

### 3. User Personas & Roles

#### 3.1 User Roles
- **Player:** Can join groups, register for games, make payments, view rosters
- **Organizer:** Can create/manage groups, post games, manage registrations, balance teams
- **Super Admin:** System-wide administration and oversight
- **Future: Tournament Organizer:** Manages tournaments (Phase 2)

*Note: Users can have multiple roles across different groups*

#### 3.2 Key User Stories

**As a Player:**
- I want to discover and request to join hockey groups
- I want to receive notifications when new games are posted in my groups
- I want to register for games and see my position (confirmed/waitlist)
- I want to see the roster and my team assignment before the game
- I want to indicate my skill level (Gold/Silver/Bronze/D-League) in my profile

**As an Organizer:**
- I want to create and manage my pickup hockey group
- I want to review and approve/reject membership requests with the ability to chat with applicants
- I want to post games with details (date, time, cost, player limit)
- I want to manage registrations and waitlists with payment tracking
- I want to balance teams using suggested compositions or manual assignment
- I want to appoint additional admins to help manage the group
- I want to open games to non-group members when registration is low

### 4. Core Features - MVP

#### 4.1 User Management
- **Registration/Login**
  - Email/password authentication
  - User profile with:
    - Name, contact info
    - Skill level (Gold/Silver/Bronze/D-League)
    - Position preferences
    - Profile photo

#### 4.2 Group Management
- **Group Creation**
  - Group name, description
  - Public/Private setting
  - Regular game schedule info
  - Organizer details

- **Membership Management**
  - Subscribe/unsubscribe to organizations ✅
  - Member list management ✅
  - **Multi-Admin System** ✅ (Implemented 2025-11-30)
    - Any admin can add/remove other admins
    - All admins have equal permissions (no owner hierarchy)
    - Cannot remove the last admin (business rule)
    - Org admins can manage all events under their organization
  - Future: Request to join with approval workflow

#### 4.3 Game Management
- **Game Creation**
  - Date, time, duration
  - Location (rink/specific sheet)
  - Cost per player
  - Maximum players (typically 20-24)
  - Registration deadline
  - Open to group only vs. public option
  - Recurring game setup with admin confirmation required

- **Registration System**
  - Quick registration for group members
  - Confirmed vs. waitlist status
  - Payment status tracking (Pending/Paid/Refunded)
  - Waitlist auto-promotion with time limit to pay
  - Drop-out functionality with waitlist notification

#### 4.4 Payment Coordination
- **Venmo Integration Approach**
  - Display organizer's Venmo handle
  - Deep link to Venmo app with pre-filled amount and note
  - Manual payment confirmation by organizer
  - Payment status tracking in app
  - Support for waitlist player paying dropped player directly

#### 4.5 Team Balancing
- **Auto-Suggestion Algorithm**
  - Balance based on skill levels
  - Consider position preferences
  - Maintain roughly equal team strengths

- **Manual Override**
  - Drag-and-drop interface for team assignment
  - Ability to lock certain players to teams
  - Save and edit team compositions

- **Roster Distribution**
  - Send final rosters on game day
  - Show Black vs. White team assignments
  - Include any last-minute changes

#### 4.6 Notifications
- Push notifications for:
  - New game posted in your group
  - Registration confirmed
  - Moved from waitlist to confirmed
  - Payment reminders
  - Roster posted
  - Game reminders (day before, day of)

### 5. Technical Architecture

#### 5.1 Tech Stack (Already Decided)
- **Frontend:** React Native (Expo)
- **Backend:** .NET
- **Real-time:** SignalR
- **Database:** TBD (suggest PostgreSQL or SQL Server)

#### 5.2 Key Data Models

```
User {
  id, email, name, phone,
  skillLevel, position,
  venmoHandle, profilePhoto,
  createdAt, updatedAt
}

Organization {
  id, name, description,
  location, skillLevel,
  creatorId, isActive,
  createdAt, updatedAt
}

OrganizationAdmin {  // Multi-admin support ✅
  id, organizationId, userId,
  addedAt, addedByUserId
}

OrganizationSubscription {
  id, organizationId, userId,
  notificationEnabled, subscribedAt
}

Event {
  id, organizationId (optional),
  creatorId, name, description,
  eventDate, duration, venue,
  maxPlayers, cost,
  registrationDeadline,
  status, visibility,
  createdAt, updatedAt
}

EventRegistration {
  id, eventId, userId,
  status (Registered/Cancelled),
  paymentStatus (Pending/MarkedPaid/Verified),
  paymentMarkedAt, paymentVerifiedAt,
  registeredAt
  // Future: waitlistPosition, promotedAt, paymentDeadline
}

ChatMessage {
  id, fromUserId, toUserId,
  groupId, message, 
  createdAt, isRead
}
```

#### 5.3 API Endpoints (Implemented)

**Authentication:** ✅
- POST /api/auth/register - Create account
- POST /api/auth/login - Get JWT token
- POST /api/auth/logout - Invalidate token
- GET /api/auth/me - Get current user

**Users:** ✅
- GET /api/users/me - Get full profile
- PUT /api/users/me - Update profile
- GET /api/users/me/organizations - Get user's admin orgs
- GET /api/users/me/events - Get user's registered events

**Organizations:** ✅
- POST /api/organizations - Create organization
- GET /api/organizations - List all organizations
- GET /api/organizations/{id} - Get organization details
- PUT /api/organizations/{id} - Update organization (admin only)
- DELETE /api/organizations/{id} - Delete organization (admin only)
- POST /api/organizations/{id}/subscribe - Subscribe to org
- DELETE /api/organizations/{id}/subscribe - Unsubscribe
- GET /api/organizations/{id}/members - List members (admin only)
- GET /api/organizations/{id}/admins - List admins (admin only) ✅
- POST /api/organizations/{id}/admins - Add admin (admin only) ✅
- DELETE /api/organizations/{id}/admins/{userId} - Remove admin ✅

**Events:** ✅
- POST /api/events - Create event
- GET /api/events - List upcoming events
- GET /api/events/{id} - Get event details
- PUT /api/events/{id} - Update event (creator/admin only)
- DELETE /api/events/{id} - Cancel event (creator/admin only)
- POST /api/events/{id}/register - Register for event
- DELETE /api/events/{id}/register - Cancel registration
- POST /api/events/{id}/payment/mark-paid - Mark payment complete ✅
- PUT /api/events/{id}/registrations/{regId}/payment - Verify payment ✅
- GET /api/events/{id}/registrations - List registrations (organizer only)

**Future - Chat:**
- POST /api/chat/send - Send message
- GET /api/chat/conversation/{userId} - Get conversation
- GET /api/chat/unread - Get unread messages

### 6. Future Features (Phase 2 & 3)

#### Phase 2: Tournament Management
- Tournament creation with multiple formats
- Team generation and management
- Bracket visualization
- Schedule generation for 2 rinks
- Results tracking
- Tournament-specific team chats
- Announcement system

#### Phase 3: League Substitute System
- Sub request posting by any league player
- Availability calendar for subs
- Quick fill system
- Rating/review system for subs

### 7. Success Metrics
- Number of active groups
- Games organized per week
- Average fill rate for games
- Time from game posting to full registration
- User retention rate

### 8. Implementation Phases

**Phase 1 - Foundation & Auth** ✅ DONE (2025-11-25)
- User registration and login
- Profile management (skill level, position, Venmo handle)
- JWT authentication with auto-logout on 401

**Phase 2 - Organizations & Subscriptions** ✅ DONE (2025-11-26)
- Organization CRUD operations
- Subscribe/unsubscribe to organizations
- Discover screen to browse organizations

**Phase 3 - Events & Registration** ✅ DONE (2025-11-26)
- Event CRUD operations (standalone or org-linked)
- Event visibility (Public, OrganizationMembers, InviteOnly)
- Registration and cancellation

**Phase 4 - Venmo Payments** ✅ DONE (2025-11-27)
- Payment status tracking (Pending, MarkedPaid, Verified)
- Venmo deep link integration
- Organizer payment verification

**Phase 4.5 - Multi-Admin Organizations** ✅ DONE (2025-11-30)
- Multiple admins per organization with equal permissions
- Org admins can manage all org events
- 33 new authorization tests

**Push Notifications** ✅ DONE (2025-11-26)
- New event notifications to org subscribers
- Expo Push API integration

**Phase 5 - Waitlist & Auto-Promotion** ⏳ NEXT
- Waitlist when events are full
- Auto-promotion with payment deadline
- Background service for deadline enforcement

**Future Phases:**
- Team balancing and roster management
- Chat system implementation
- Tournament management
- League substitute finder

### 9. Open Questions & Considerations

1. **Venmo API Limitations:** Consider backup payment coordination methods
2. **Group Size Limits:** Should there be maximum members per group?
3. **Historical Data:** How long to retain game/roster history?
4. **Dispute Resolution:** Process for payment disputes or no-shows?
5. **Skill Verification:** Any validation of self-reported skill levels?

### 10. Non-Functional Requirements
- App should work offline for viewing (sync when connected)
- Response time < 2 seconds for all operations
- Support 100+ concurrent users
- Data encryption for sensitive information
- GDPR compliance for user data

---

This PRD provides a comprehensive foundation for your engineering team. Would you like me to elaborate on any section, create user flow diagrams, or develop more detailed technical specifications for specific features?
