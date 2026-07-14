using Habitrack.Api.Repositories;

namespace Habitrack.Api.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    IHabitRepository Habits { get; }
    IUserRepository Users { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
