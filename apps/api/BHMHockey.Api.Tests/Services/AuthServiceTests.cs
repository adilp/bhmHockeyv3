using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for AuthService - the most security-critical code in the app.
/// These tests protect our authentication system.
/// </summary>
public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly AuthService _sut; // System Under Test

    public AuthServiceTests()
    {
        // Create in-memory database with unique name for test isolation
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        // Setup JWT configuration
        _mockConfig = new Mock<IConfiguration>();
        _mockConfig.Setup(c => c["Jwt:Secret"]).Returns("test-secret-key-that-is-at-least-32-characters-long!");
        _mockConfig.Setup(c => c["Jwt:Issuer"]).Returns("test-issuer");
        _mockConfig.Setup(c => c["Jwt:Audience"]).Returns("test-audience");
        _mockConfig.Setup(c => c["Jwt:ExpiryMinutes"]).Returns("60");

        _sut = new AuthService(_context, _mockConfig.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Registration Tests

    [Fact]
    public async Task RegisterAsync_WithValidData_CreatesUserAndReturnsToken()
    {
        // Arrange
        var request = new RegisterRequest("test@example.com", "Password1!", "John", "Doe", null, null, null);

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
        result.User.Email.Should().Be("test@example.com");
        result.User.FirstName.Should().Be("John");
        result.User.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new RegisterRequest("dupe@example.com", "Password1!", "John", "Doe", null, null, null);
        await _sut.RegisterAsync(request);

        // Act
        var act = () => _sut.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task RegisterAsync_HashesPassword_NeverStoresPlaintext()
    {
        // Arrange
        var plainPassword = "Password1!";
        var request = new RegisterRequest("hash@example.com", plainPassword, "John", "Doe", null, null, null);

        // Act
        await _sut.RegisterAsync(request);
        var user = await _context.Users.FirstAsync(u => u.Email == "hash@example.com");

        // Assert
        user.PasswordHash.Should().NotBe(plainPassword);
        BCrypt.Net.BCrypt.Verify(plainPassword, user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_GeneratesUniqueRefreshToken()
    {
        // Arrange
        var request1 = new RegisterRequest("user1@example.com", "Password1!", "User", "One", null, null, null);
        var request2 = new RegisterRequest("user2@example.com", "Password1!", "User", "Two", null, null, null);

        // Act
        var result1 = await _sut.RegisterAsync(request1);
        var result2 = await _sut.RegisterAsync(request2);

        // Assert
        result1.RefreshToken.Should().NotBeNullOrEmpty();
        result2.RefreshToken.Should().NotBeNullOrEmpty();
        result1.RefreshToken.Should().NotBe(result2.RefreshToken);
    }

    [Fact]
    public async Task RegisterAsync_SetsUserRoleToPlayer()
    {
        // Arrange
        var request = new RegisterRequest("role@example.com", "Password1!", "John", "Doe", null, null, null);

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.User.Role.Should().Be("Player");
    }

    [Fact]
    public async Task RegisterAsync_CreatesActiveUser()
    {
        // Arrange
        var request = new RegisterRequest("active@example.com", "Password1!", "John", "Doe", null, null, null);

        // Act
        await _sut.RegisterAsync(request);
        var user = await _context.Users.FirstAsync(u => u.Email == "active@example.com");

        // Assert
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_WithPhoneNumber_StoresPhoneNumber()
    {
        // Arrange
        var request = new RegisterRequest("phone@example.com", "Password1!", "John", "Doe", "1234567890", null, null);

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.User.PhoneNumber.Should().Be("1234567890");
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        await _sut.RegisterAsync(new RegisterRequest("login@example.com", "Password1!", "John", "Doe", null, null, null));
        var loginRequest = new LoginRequest("login@example.com", "Password1!");

        // Act
        var result = await _sut.LoginAsync(loginRequest);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
        result.User.Email.Should().Be("login@example.com");
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsUnauthorizedException()
    {
        // Arrange
        await _sut.RegisterAsync(new RegisterRequest("wrong@example.com", "Password1!", "John", "Doe", null, null, null));

        // Act
        var act = () => _sut.LoginAsync(new LoginRequest("wrong@example.com", "WrongPassword!"));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentEmail_ThrowsUnauthorizedException()
    {
        // Act
        var act = () => _sut.LoginAsync(new LoginRequest("nonexistent@example.com", "Password1!"));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LoginAsync_WithInactiveAccount_ThrowsUnauthorizedException()
    {
        // Arrange
        await _sut.RegisterAsync(new RegisterRequest("inactive@example.com", "Password1!", "John", "Doe", null, null, null));
        var user = await _context.Users.FirstAsync(u => u.Email == "inactive@example.com");
        user.IsActive = false;
        await _context.SaveChangesAsync();

        // Act
        var act = () => _sut.LoginAsync(new LoginRequest("inactive@example.com", "Password1!"));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*deleted*");
    }

    #endregion

    #region JWT Token Tests

    [Fact]
    public async Task GenerateJwtToken_ContainsRequiredClaims()
    {
        // Arrange
        var request = new RegisterRequest("claims@example.com", "Password1!", "John", "Doe", null, null, null);

        // Act
        var result = await _sut.RegisterAsync(request);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);

        // Assert
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub);
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "claims@example.com");
        token.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Player");
    }

    [Fact]
    public async Task GenerateJwtToken_HasCorrectExpiry()
    {
        // Arrange
        var request = new RegisterRequest("expiry@example.com", "Password1!", "John", "Doe", null, null, null);

        // Act
        var result = await _sut.RegisterAsync(request);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);

        // Assert
        // Token should expire in approximately 60 minutes (with some tolerance for test execution time)
        var expectedExpiry = DateTime.UtcNow.AddMinutes(60);
        token.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GenerateJwtToken_WithMissingSecret_ThrowsException()
    {
        // Arrange
        var mockConfigNoSecret = new Mock<IConfiguration>();
        mockConfigNoSecret.Setup(c => c["Jwt:Secret"]).Returns((string?)null);
        var serviceNoSecret = new AuthService(_context, mockConfigNoSecret.Object);
        var request = new RegisterRequest("nosecret@example.com", "Password1!", "John", "Doe", null, null, null);

        // Add user to database first (registration will fail at token generation)
        _context.Users.Add(new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = "Player"
        });
        await _context.SaveChangesAsync();

        // Act
        var act = () => serviceNoSecret.RegisterAsync(new RegisterRequest("nosecret2@example.com", "Password1!", "Test", "User", null, null, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*JWT Secret*");
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task LogoutAsync_ReturnsTrue()
    {
        // Arrange
        var registerResult = await _sut.RegisterAsync(new RegisterRequest("logout@example.com", "Password1!", "John", "Doe", null, null, null));

        // Act
        var result = await _sut.LogoutAsync(registerResult.User.Id);

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}
