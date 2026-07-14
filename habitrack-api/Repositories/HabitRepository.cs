using Habitrack.Api.Data;
using Habitrack.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Habitrack.Api.Repositories;

public class HabitRepository : IHabitRepository
{
    private readonly AppDbContext _db;

    public HabitRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Habit>> GetActiveByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.Habits
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Habit?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Habits.FindAsync([id], ct);
    }

    public async Task<Habit?> GetByIdWithCompletionsAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Habits
            .Include(h => h.Completions.OrderByDescending(c => c.Date))
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(h => h.Id == id, ct);
    }

    public async Task<bool> HasCompletionOnDateAsync(Guid habitId, DateOnly date, CancellationToken ct = default)
    {
        return await _db.HabitCompletions
            .AnyAsync(c => c.HabitId == habitId && c.Date == date, ct);
    }

    public async Task<List<HabitCompletion>> GetCompletionsAsync(Guid habitId, CancellationToken ct = default)
    {
        return await _db.HabitCompletions
            .Where(c => c.HabitId == habitId)
            .OrderByDescending(c => c.Date)
            .ToListAsync(ct);
    }

    public void Add(Habit habit) => _db.Habits.Add(habit);
    public void Update(Habit habit) => _db.Habits.Update(habit);

    public void AddCompletion(HabitCompletion completion) => _db.HabitCompletions.Add(completion);
    public void RemoveCompletion(HabitCompletion completion) => _db.HabitCompletions.Remove(completion);
}
