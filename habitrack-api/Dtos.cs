namespace Habitrack.Api.Dtos;

public record HabitDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public int WeeklyFrequency { get; init; }
    public string Color { get; init; } = null!;
    public bool IsActive { get; init; }
    public int CurrentStreak { get; init; }
    public int BestStreak { get; init; }
    public int TotalCompletions { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CreateHabitRequest
{
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public int WeeklyFrequency { get; init; }
    public string? Color { get; init; }
}

public record UpdateHabitRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public int? WeeklyFrequency { get; init; }
    public string? Color { get; init; }
}

public record HabitCompletionDto
{
    public Guid Id { get; init; }
    public Guid HabitId { get; init; }
    public DateOnly Date { get; init; }
    public DateTime CompletedAt { get; init; }
}

public record CreateCompletionRequest
{
    public DateOnly Date { get; init; }
}

public record RegisterRequest
{
    public string Email { get; init; } = null!;
    public string Password { get; init; } = null!;
}

public record LoginRequest
{
    public string Email { get; init; } = null!;
    public string Password { get; init; } = null!;
}

public record AuthResponse
{
    public string Token { get; init; } = null!;
    public UserDto User { get; init; } = null!;
}

public record UserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = null!;
}
