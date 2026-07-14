using Habitrack.Api.Models;

namespace Habitrack.Api.Repositories;

public interface IHabitRepository
{
    Task<IEnumerable<Habit>> GetActiveByUserAsync(Guid userId, CancellationToken ct = default);
    Task<Habit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Habit?> GetByIdWithCompletionsAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasCompletionOnDateAsync(Guid habitId, DateOnly date, CancellationToken ct = default);
    Task<List<HabitCompletion>> GetCompletionsAsync(Guid habitId, CancellationToken ct = default);
    void Add(Habit habit);
    void Update(Habit habit);
    void AddCompletion(HabitCompletion completion);
    void RemoveCompletion(HabitCompletion completion);
}
