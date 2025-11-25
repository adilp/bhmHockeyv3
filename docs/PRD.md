Excellent! Based on your answers, let me create a comprehensive PRD for your hockey pickup game management app. I'll focus on the MVP (pickup groups) with considerations for future features.

# Product Requirements Document
## Hockey Pickup Game Management App

### 1. Executive Summary
A community-focused mobile application for organizing, managing, and participating in hockey pickup games at a local rink. The app streamlines the current manual process of organizing games via messaging and Venmo payments, providing a centralized platform for game management, team balancing, and payment coordination.

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
  - Request to join system
  - Approval workflow with in-app chat between organizer and applicant
  - Member list management
  - Admin appointment functionality
  - Member removal capability

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

Group {
  id, name, description,
  isPublic, organizerId,
  adminIds[], memberIds[],
  pendingMemberIds[],
  createdAt, updatedAt
}

Game {
  id, groupId, organizerId,
  dateTime, duration, location,
  costPerPlayer, maxPlayers,
  registrationDeadline,
  isOpenToPublic, recurringId,
  status, createdAt, updatedAt
}

Registration {
  id, gameId, userId,
  status (confirmed/waitlist/cancelled),
  paymentStatus, teamAssignment,
  registeredAt, updatedAt
}

ChatMessage {
  id, fromUserId, toUserId,
  groupId, message, 
  createdAt, isRead
}
```

#### 5.3 API Endpoints (Key Examples)

**Groups:**
- POST /api/groups - Create group
- GET /api/groups/search - Find groups
- POST /api/groups/{id}/join - Request to join
- POST /api/groups/{id}/approve - Approve member
- POST /api/groups/{id}/admins - Add admin

**Games:**
- POST /api/games - Create game
- GET /api/groups/{id}/games - List group games
- POST /api/games/{id}/register - Register for game
- PUT /api/games/{id}/payment - Update payment status
- POST /api/games/{id}/teams - Save team composition
- POST /api/games/{id}/roster - Send roster

**Chat:**
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

**Phase 1 - MVP (Weeks 1-8)**
- Week 1-2: User authentication and profile management
- Week 3-4: Group creation and membership management
- Week 5-6: Game creation and registration system
- Week 7: Team balancing and roster management
- Week 8: Notifications and Venmo payment coordination

**Phase 2 - Enhanced Features (Weeks 9-12)**
- Chat system implementation
- Recurring games with confirmation
- Public game discovery
- Payment tracking improvements

**Phase 3 - Tournament Features (Weeks 13-16)**
- Tournament setup and management
- Bracket generation and visualization
- Tournament communication tools

**Phase 4 - League Features (Weeks 17-18)**
- Substitute finder system
- League integration

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
