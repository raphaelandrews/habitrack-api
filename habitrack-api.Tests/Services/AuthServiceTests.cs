using Habitrack.Api.Dtos;
using Habitrack.Api.Models;
using Habitrack.Api.Repositories;
using Habitrack.Api.Services;
using Habitrack.Api.UnitOfWork;
using Microsoft.Extensions.Options;
using Moq;

namespace Habitrack.Api.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly JwtSettings _jwtSettings;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserRepository>();

        _uowMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

        _jwtSettings = new JwtSettings
        {
            Secret = "test-secret-key-with-at-least-32-characters-for-hmac-sha256!",
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 60
        };

        var options = Options.Create(_jwtSettings);
        _service = new AuthService(_uowMock.Object, options);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_CreatesUserAndReturnsToken()
    {
        var request = new RegisterRequest
        {
            Email = "newuser@example.com",
            Password = "Senha@123"
        };

        _userRepoMock
            .Setup(r => r.EmailExistsAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        User? captured = null;
        _userRepoMock
            .Setup(r => r.Add(It.IsAny<User>()))
            .Callback<User>(u => captured = u);

        var result = await _service.RegisterAsync(request);

        Assert.NotNull(result);
        Assert.NotNull(result!.Token);
        Assert.Equal(request.Email, result.User.Email);

        Assert.NotNull(captured);
        Assert.Equal(request.Email, captured!.Email);
        Assert.NotEmpty(captured.PasswordHash);
        Assert.NotEqual(request.Password, captured.PasswordHash);

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsNull()
    {
        _userRepoMock
            .Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.RegisterAsync(new RegisterRequest
        {
            Email = "exists@example.com",
            Password = "Senha@123"
        });

        Assert.Null(result);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        var email = "user@example.com";
        var password = "CorrectPassword";
        var hash = HashPasswordForTest(password);

        _userRepoMock
            .Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = hash
            });

        var result = await _service.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = password
        });

        Assert.NotNull(result);
        Assert.NotNull(result!.Token);
        Assert.Equal(email, result.User.Email);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        var email = "user@example.com";
        var hash = HashPasswordForTest("CorrectPassword");

        _userRepoMock
            .Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = hash
            });

        var result = await _service.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = "WrongPassword"
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ReturnsNull()
    {
        _userRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _service.LoginAsync(new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "whatever"
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_GeneratesDifferentTokensForDifferentUsers()
    {
        var user1 = new User { Id = Guid.NewGuid(), Email = "a@test.com", PasswordHash = HashPasswordForTest("pwd") };
        var user2 = new User { Id = Guid.NewGuid(), Email = "b@test.com", PasswordHash = HashPasswordForTest("pwd") };

        _userRepoMock
            .Setup(r => r.GetByEmailAsync("a@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user1);

        var result1 = await _service.LoginAsync(new LoginRequest { Email = "a@test.com", Password = "pwd" });

        _userRepoMock
            .Setup(r => r.GetByEmailAsync("b@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user2);

        var result2 = await _service.LoginAsync(new LoginRequest { Email = "b@test.com", Password = "pwd" });

        Assert.NotEqual(result1!.Token, result2!.Token);
    }

    private static string HashPasswordForTest(string password)
    {
        byte[] salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        byte[] hash = Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivationPrf.HMACSHA256,
            iterationCount: 100_000,
            numBytesRequested: 32);

        byte[] result = new byte[48];
        Buffer.BlockCopy(salt, 0, result, 0, 16);
        Buffer.BlockCopy(hash, 0, result, 16, 32);
        return Convert.ToBase64String(result);
    }
}
