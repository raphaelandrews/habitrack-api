using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Habitrack.Api.Dtos;
using Habitrack.Api.Models;
using Habitrack.Api.UnitOfWork;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Habitrack.Api.Services;

public class HabitService
{
    private readonly IUnitOfWork _uow;

    public HabitService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<HabitDto[]> GetAllAsync(Guid userId, CancellationToken ct = default)
    {
        var habits = await _uow.Habits.GetActiveByUserAsync(userId, ct);
        return habits.Select(h => MapToDto(h, h.Completions)).ToArray();
    }

    public async Task<HabitDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var habit = await _uow.Habits.GetByIdWithCompletionsAsync(id, ct);
        if (habit is null || habit.UserId != userId)
            return null;

        return MapToDto(habit, habit.Completions);
    }

    public async Task<HabitDto> CreateAsync(CreateHabitRequest request, Guid userId, CancellationToken ct = default)
    {
        var habit = new Habit
        {
            Name = request.Name,
            Description = request.Description,
            WeeklyFrequency = request.WeeklyFrequency,
            Color = request.Color ?? "#3B82F6",
            UserId = userId
        };

        _uow.Habits.Add(habit);
        await _uow.SaveChangesAsync(ct);

        return MapToDto(habit, new List<HabitCompletion>());
    }

    public async Task<HabitDto?> UpdateAsync(Guid id, UpdateHabitRequest request, Guid userId, CancellationToken ct = default)
    {
        var habit = await _uow.Habits.GetByIdAsync(id, ct);
        if (habit is null || habit.UserId != userId)
            return null;

        if (request.Name is not null) habit.Name = request.Name;
        if (request.Description is not null) habit.Description = request.Description;
        if (request.WeeklyFrequency.HasValue) habit.WeeklyFrequency = request.WeeklyFrequency.Value;
        if (request.Color is not null) habit.Color = request.Color;

        _uow.Habits.Update(habit);
        await _uow.SaveChangesAsync(ct);

        var completions = await _uow.Habits.GetCompletionsAsync(id, ct);
        return MapToDto(habit, completions);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var habit = await _uow.Habits.GetByIdAsync(id, ct);
        if (habit is null || habit.UserId != userId)
            return false;

        habit.IsActive = false;
        habit.EndedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<HabitCompletionDto?> MarkCompleteAsync(Guid habitId, CreateCompletionRequest request,
        Guid userId, CancellationToken ct = default)
    {
        var habit = await _uow.Habits.GetByIdAsync(habitId, ct);
        if (habit is null || habit.UserId != userId)
            return null;

        var alreadyCompleted = await _uow.Habits.HasCompletionOnDateAsync(habitId, request.Date, ct);
        if (alreadyCompleted)
        {
            var completions = await _uow.Habits.GetCompletionsAsync(habitId, ct);
            var existing = completions.First(c => c.Date == request.Date);
            return existing.ToDto();
        }

        var completion = new HabitCompletion
        {
            HabitId = habitId,
            UserId = userId,
            Date = request.Date
        };

        _uow.Habits.AddCompletion(completion);
        await _uow.SaveChangesAsync(ct);
        return completion.ToDto();
    }

    public async Task<bool> UnmarkCompleteAsync(Guid habitId, DateOnly date, Guid userId,
        CancellationToken ct = default)
    {
        var habit = await _uow.Habits.GetByIdAsync(habitId, ct);
        if (habit is null || habit.UserId != userId)
            return false;

        var completions = await _uow.Habits.GetCompletionsAsync(habitId, ct);
        var toRemove = completions.FirstOrDefault(c => c.Date == date);
        if (toRemove is null)
            return false;

        _uow.Habits.RemoveCompletion(toRemove);
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<HabitCompletionDto[]> GetCompletionsAsync(Guid habitId, Guid userId,
        CancellationToken ct = default)
    {
        var habit = await _uow.Habits.GetByIdAsync(habitId, ct);
        if (habit is null || habit.UserId != userId)
            return Array.Empty<HabitCompletionDto>();

        var completions = await _uow.Habits.GetCompletionsAsync(habitId, ct);
        return completions.Select(c => c.ToDto()).ToArray();
    }

    private static HabitDto MapToDto(Habit h, IEnumerable<HabitCompletion> completions)
    {
        var (current, best) = CalculateStreaks(completions, h.WeeklyFrequency);

        return new HabitDto
        {
            Id = h.Id,
            Name = h.Name,
            Description = h.Description,
            WeeklyFrequency = h.WeeklyFrequency,
            Color = h.Color,
            IsActive = h.IsActive,
            CurrentStreak = current,
            BestStreak = best,
            TotalCompletions = completions.Count(),
            CreatedAt = h.CreatedAt
        };
    }

    private static (int current, int best) CalculateStreaks(
        IEnumerable<HabitCompletion> completions, int weeklyFrequency)
    {
        var dates = completions
            .Select(c => c.Date)
            .OrderByDescending(d => d)
            .ToList();

        if (dates.Count == 0)
            return (0, 0);

        int current = 0, best = 0, streak = 1;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (dates[0] == today || dates[0] == today.AddDays(-1))
        {
            current = 1;
            for (int i = 1; i < dates.Count; i++)
            {
                if (dates[i] == dates[i - 1].AddDays(-1))
                    current++;
                else
                    break;
            }
        }

        for (int i = 1; i < dates.Count; i++)
        {
            if (dates[i] == dates[i - 1].AddDays(-1))
                streak++;
            else
            {
                best = Math.Max(best, streak);
                streak = 1;
            }
        }
        best = Math.Max(best, streak);

        return (current, best);
    }
}

public class AuthService
{
    private readonly IUnitOfWork _uow;
    private readonly JwtSettings _jwtSettings;

    public AuthService(IUnitOfWork uow, IOptions<JwtSettings> jwtSettings)
    {
        _uow = uow;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await _uow.Users.EmailExistsAsync(request.Email, ct))
            return null;

        var user = new User
        {
            Email = request.Email,
            PasswordHash = HashPassword(request.Password)
        };

        _uow.Users.Add(user);
        await _uow.SaveChangesAsync(ct);

        return new AuthResponse
        {
            Token = GenerateToken(user),
            User = new UserDto { Id = user.Id, Email = user.Email }
        };
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByEmailAsync(request.Email, ct);
        if (user is null || !VerifyPassword(request.Password, user.PasswordHash))
            return null;

        return new AuthResponse
        {
            Token = GenerateToken(user),
            User = new UserDto { Id = user.Id, Email = user.Email }
        };
    }

    private string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100_000,
            numBytesRequested: 32);

        byte[] result = new byte[48];
        Buffer.BlockCopy(salt, 0, result, 0, 16);
        Buffer.BlockCopy(hash, 0, result, 16, 32);
        return Convert.ToBase64String(result);
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        byte[] hashBytes = Convert.FromBase64String(storedHash);
        byte[] salt = new byte[16];
        byte[] storedPasswordHash = new byte[32];

        Buffer.BlockCopy(hashBytes, 0, salt, 0, 16);
        Buffer.BlockCopy(hashBytes, 16, storedPasswordHash, 0, 32);

        byte[] computedHash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100_000,
            numBytesRequested: 32);

        return CryptographicOperations.FixedTimeEquals(storedPasswordHash, computedHash);
    }
}

public class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Secret { get; init; } = null!;
    public string Issuer { get; init; } = null!;
    public string Audience { get; init; } = null!;
    public int ExpirationMinutes { get; init; } = 60;
}

public static class HabitCompletionExtensions
{
    public static HabitCompletionDto ToDto(this HabitCompletion c) =>
        new HabitCompletionDto
        {
            Id = c.Id,
            HabitId = c.HabitId,
            Date = c.Date,
            CompletedAt = c.CompletedAt
        };
}
