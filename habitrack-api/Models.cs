namespace Habitrack.Api.Models;

public class Habit
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int WeeklyFrequency { get; set; } = 7; // 1-7, padrão = todos os dias
    public string Color { get; set; } = "#3B82F6";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    // Navegação
    public List<HabitCompletion> Completions { get; set; } = new();
}

public class HabitCompletion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid HabitId { get; init; }
    public Guid UserId { get; init; }
    public DateOnly Date { get; init; }
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;

    public Habit? Habit { get; set; }
}

public class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
