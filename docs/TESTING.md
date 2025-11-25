# BHM Hockey Testing Philosophy

## Core Belief

**Tests exist to give us confidence to change code.**

A test suite that doesn't let you refactor fearlessly is a liability, not an asset. Every test we write should answer one question: "Will this catch a bug that matters?"

---

## The Three Questions

Before writing any test, ask:

1. **What could break that would hurt users?**
   - If this code fails, does someone's registration fail? Does their payment not process? Do they miss an event?
   - If the answer is "nothing important," don't write the test.

2. **Would I notice this break in development?**
   - If a typo in a button label would be caught in 5 seconds of manual testing, a unit test adds no value.
   - Save tests for things that are hard to verify manually or could silently fail.

3. **Will this test survive refactoring?**
   - If changing implementation details (variable names, internal method structure) breaks the test, it's testing the wrong thing.
   - Test behavior and outcomes, not how you got there.

---

## What We Test

### Always Test

| Category | Why | Example |
|----------|-----|---------|
| **Authentication & Authorization** | Security failures are catastrophic | "User with expired token cannot access protected routes" |
| **Data integrity** | Corrupted data destroys trust | "Partial profile update doesn't overwrite unrelated fields" |
| **Money and payments** | Financial bugs are unacceptable | "Registration fee is calculated correctly for event" |
| **Business rules** | These define the product | "User cannot RSVP to full event without joining waitlist" |
| **Error boundaries** | Users shouldn't see crashes | "Invalid input returns 400, not 500" |
| **State transitions** | Race conditions hide here | "Logout clears all auth state atomically" |

### Sometimes Test

| Category | When | Example |
|----------|------|---------|
| **Complex transformations** | When logic has edge cases | "Date formatting handles timezone boundaries" |
| **Validation rules** | When rules are non-obvious | "Password requires special character" |
| **Integration points** | When contracts matter | "API client stores token after login" |

### Never Test

| Category | Why Not | Instead Do |
|----------|---------|------------|
| **Framework behavior** | React/ASP.NET are already tested | Trust the framework |
| **Simple getters/setters** | Zero logic, zero risk | Visual inspection |
| **Implementation details** | Breaks on refactor | Test the public interface |
| **Third-party libraries** | Not our responsibility | Integration tests if critical |
| **Trivial code** | Wastes maintenance time | Code review |

---

## Testing Principles

### 1. Test Behavior, Not Implementation

```typescript
// BAD: Tests implementation details
it('calls setUser with the response', () => {
  login(creds);
  expect(setState).toHaveBeenCalledWith({ user: mockUser });
});

// GOOD: Tests observable behavior
it('authenticates user and updates auth state', async () => {
  await login(creds);
  expect(store.getState().isAuthenticated).toBe(true);
  expect(store.getState().user.email).toBe('test@example.com');
});
```

### 2. One Reason to Fail

Each test should fail for exactly one reason. If a test can fail because of multiple unrelated issues, split it.

```csharp
// BAD: Multiple failure reasons
[Fact]
public async Task Register_Works()
{
    var result = await _authService.RegisterAsync(request);
    Assert.NotNull(result);
    Assert.NotNull(result.Token);
    Assert.NotNull(result.User);
    Assert.Equal("test@example.com", result.User.Email);
    Assert.True(result.User.IsActive);
}

// GOOD: Focused assertions
[Fact]
public async Task Register_ReturnsValidJwtToken()
{
    var result = await _authService.RegisterAsync(request);
    Assert.NotNull(result.Token);
    Assert.True(IsValidJwt(result.Token));
}

[Fact]
public async Task Register_CreatesActiveUser()
{
    var result = await _authService.RegisterAsync(request);
    Assert.True(result.User.IsActive);
}
```

### 3. Descriptive Test Names

Test names should describe the scenario and expected outcome. Someone should understand what broke just from the test name.

```
Format: [Method]_[Scenario]_[ExpectedResult]

✓ LoginAsync_WithValidCredentials_ReturnsAuthResponse
✓ LoginAsync_WithWrongPassword_ThrowsUnauthorizedException
✓ LoginAsync_WithInactiveAccount_ThrowsUnauthorizedException
✓ UpdateProfile_WithNullFirstName_PreservesExistingFirstName
```

### 4. Arrange-Act-Assert

Every test follows the same structure:

```csharp
[Fact]
public async Task UpdateProfile_WithPartialData_OnlyUpdatesProvidedFields()
{
    // Arrange: Set up preconditions
    var user = await CreateTestUser(firstName: "John", lastName: "Doe");
    var request = new UpdateUserProfileRequest { FirstName = "Jane" };

    // Act: Execute the behavior under test
    var result = await _userService.UpdateProfileAsync(user.Id, request);

    // Assert: Verify the outcome
    Assert.Equal("Jane", result.FirstName);  // Changed
    Assert.Equal("Doe", result.LastName);    // Preserved
}
```

### 5. Test Isolation

Tests must not depend on each other or shared state. Each test:
- Creates its own test data
- Cleans up after itself (or uses fresh in-memory DB)
- Can run in any order
- Can run in parallel

### 6. Fast Feedback

Unit tests should be fast. If a test takes more than 100ms, question whether it's actually a unit test or an integration test in disguise.

---

## The Test Pyramid

```
        /\
       /  \      E2E Tests (Few)
      /    \     - Full user journeys
     /------\    - Run before deploy
    /        \
   /          \  Integration Tests (Some)
  /            \ - API endpoint tests
 /--------------\- Database interaction
/                \
/                  \ Unit Tests (Many)
/                    \- Business logic
/______________________\- Pure functions
```

### Our Focus for Phase 1

**Primarily Unit Tests** because:
- Fast to write and run
- Easy to maintain
- Cover the critical business logic
- Give immediate feedback during development

**Limited Integration Tests** for:
- Auth flow end-to-end
- Profile update persistence

---

## Regression Testing Philosophy

### Every Bug Gets a Test

When a bug is found:
1. Write a failing test that reproduces the bug
2. Fix the bug
3. Verify the test passes
4. The test stays forever

This ensures the same bug never returns.

### Tests as Documentation

Tests document expected behavior. When someone asks "what happens if X?", the answer should be in a test:

```typescript
// This test IS the documentation for edge case handling
it('logout clears auth state even if API call fails', async () => {
  authService.logout.mockRejectedValue(new Error('Network error'));

  await store.getState().logout();

  expect(store.getState().isAuthenticated).toBe(false);
  expect(store.getState().user).toBeNull();
});
```

---

## What We Don't Do

### No Test Coverage Targets

We don't chase percentages. 80% coverage with meaningless tests is worse than 40% coverage of critical paths. Coverage is a tool, not a goal.

### No Testing Private Methods

If you feel the need to test a private method:
1. It might need to be extracted to its own unit
2. Or it should be tested through the public interface
3. Or it's not important enough to test

### No Mocking Everything

Over-mocking creates tests that pass even when the real system is broken. Mock external dependencies (database, APIs), not internal collaborators.

### No Test-Induced Design Damage

Don't make code worse just to make it testable. If something is hard to test, that's feedback about the design—but the solution isn't always to add seams and interfaces everywhere.

---

## Test Infrastructure

### Project Structure

```
bhmhockey2/
├── apps/
│   ├── api/
│   │   ├── BHMHockey.Api/           # Main API project
│   │   └── BHMHockey.Api.Tests/     # Backend tests ← WRITE .NET TESTS HERE
│   │       └── *.cs
│   └── mobile/
│       ├── __tests__/               # Mobile app tests ← WRITE RN TESTS HERE
│       │   └── *.test.tsx
│       ├── __mocks__/               # React Native mocks
│       │   └── react-native.js
│       ├── jest.config.js
│       └── jest.setup.js
├── packages/
│   ├── shared/
│   │   ├── __tests__/               # Shared utils tests ← WRITE SHARED TESTS HERE
│   │   │   └── *.test.ts
│   │   └── jest.config.js
│   └── api-client/
│       ├── __tests__/               # API client tests ← WRITE CLIENT TESTS HERE
│       │   └── *.test.ts
│       └── jest.config.js
└── package.json                     # Root test scripts
```

### Which Tests Go Where

| Test Type | Location | What to Test |
|-----------|----------|--------------|
| **Backend Unit Tests** | `apps/api/BHMHockey.Api.Tests/` | Services, business logic, validation, auth |
| **Shared Package Tests** | `packages/shared/__tests__/` | Utility functions, type guards, constants validation |
| **API Client Tests** | `packages/api-client/__tests__/` | Service methods, interceptors, error handling |
| **Mobile App Tests** | `apps/mobile/__tests__/` | Components, hooks, stores, screens |

### Backend Tests (.NET)

**Location:** `apps/api/BHMHockey.Api.Tests/`

Tests for services, controllers, and business logic. Uses in-memory database for data layer tests.

```csharp
// Example: apps/api/BHMHockey.Api.Tests/Services/AuthServiceTests.cs
using FluentAssertions;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var mockUserRepo = new Mock<IUserRepository>();
        var service = new AuthService(mockUserRepo.Object);

        // Act
        var result = await service.LoginAsync("test@example.com", "password");

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeEmpty();
    }
}
```

### Shared Package Tests

**Location:** `packages/shared/__tests__/`

Tests for utility functions, validation helpers, and shared business logic.

```typescript
// Example: packages/shared/__tests__/utils/validation.test.ts
import { isValidEmail, isValidPassword } from '../../src/utils/validation';

describe('Validation Utils', () => {
  describe('isValidEmail', () => {
    it('returns true for valid email', () => {
      expect(isValidEmail('user@example.com')).toBe(true);
    });

    it('returns false for invalid email', () => {
      expect(isValidEmail('not-an-email')).toBe(false);
    });
  });
});
```

### API Client Tests

**Location:** `packages/api-client/__tests__/`

Tests for API service methods, request/response handling, and auth token management.

```typescript
// Example: packages/api-client/__tests__/services/auth.test.ts
import { authService } from '../../src/services/auth';

describe('AuthService', () => {
  it('stores token after successful login', async () => {
    // Mock axios and AsyncStorage
    const result = await authService.login('test@example.com', 'password');
    expect(result.token).toBeDefined();
  });
});
```

### Mobile App Tests

**Location:** `apps/mobile/__tests__/`

Tests for React Native components, hooks, Zustand stores, and screen logic.

```typescript
// Example: apps/mobile/__tests__/stores/auth.test.ts
import { useAuthStore } from '../../stores/auth';

describe('Auth Store', () => {
  beforeEach(() => {
    useAuthStore.getState().reset();
  });

  it('sets user on login', () => {
    const { login } = useAuthStore.getState();
    login({ id: '1', email: 'test@example.com' });

    expect(useAuthStore.getState().user?.email).toBe('test@example.com');
    expect(useAuthStore.getState().isAuthenticated).toBe(true);
  });
});
```

**Note:** Mobile tests use a custom Jest config (not jest-expo) for React 19 compatibility. React Native is mocked via `__mocks__/react-native.js`.

---

## Testing Frameworks

### Backend (.NET 8)

| Framework | Purpose | Why This One |
|-----------|---------|--------------|
| **xUnit** | Test runner | Modern, parallel-by-default, clean syntax |
| **Moq** | Mocking | Industry standard, intuitive API |
| **FluentAssertions** | Assertions | Readable, helpful failure messages |
| **Microsoft.EntityFrameworkCore.InMemory** | DB testing | Fast, no external dependencies |

### Frontend (TypeScript/React Native)

| Framework | Purpose | Why This One |
|-----------|---------|--------------|
| **Jest** | Test runner | Standard for JS/TS, great DX |
| **ts-jest** | TypeScript (shared, api-client) | Seamless TS support for node packages |
| **babel-jest** | TypeScript (mobile) | Better React 19 compatibility |
| **@testing-library/react-native** | Components | Tests user behavior, not implementation |

---

## Running Tests

### Quick Reference

```bash
# Run ALL tests (backend + frontend)
yarn test

# Backend only
yarn test:api

# All frontend packages
yarn test:frontend

# Individual packages
yarn test:shared
yarn test:api-client
yarn test:mobile

# Watch mode (mobile - great for TDD)
yarn test:watch

# With coverage
yarn test:coverage
```

### Detailed Commands

```bash
# Backend with verbose output
cd apps/api/BHMHockey.Api.Tests && dotnet test --verbosity normal

# Backend with coverage
cd apps/api/BHMHockey.Api.Tests && dotnet test --collect:"XPlat Code Coverage"

# Single test file (frontend)
yarn workspace @bhmhockey/mobile test -- __tests__/stores/auth.test.ts

# Update snapshots (if using)
yarn workspace @bhmhockey/mobile test -- -u

# Run tests matching pattern
yarn workspace @bhmhockey/shared test -- --testNamePattern="validation"
```

### CI/CD Integration

```bash
# For CI pipelines - runs all tests with single exit code
yarn test

# Expected output on success:
# Backend:     4 passed
# Shared:      3 passed
# API Client:  3 passed
# Mobile:      4 passed
```

---

## Summary

Write tests that:
- Catch bugs that matter
- Survive refactoring
- Document expected behavior
- Run fast
- Fail for one reason

Don't write tests that:
- Prove the obvious
- Test framework code
- Chase coverage numbers
- Break when implementation changes
- Take forever to run

**The goal is confidence, not coverage.**
