using Habitrack.Api.Data;
using Habitrack.Api.Repositories;

namespace Habitrack.Api.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    private IHabitRepository? _habits;
    private IUserRepository? _users;

    public UnitOfWork(AppDbContext db)
    {
        _db = db;
    }

    public IHabitRepository Habits =>
        _habits ??= new HabitRepository(_db);

    public IUserRepository Users =>
        _users ??= new UserRepository(_db);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _db.SaveChangesAsync(ct);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
