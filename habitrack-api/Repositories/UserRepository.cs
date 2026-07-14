using Habitrack.Api.Data;
using Habitrack.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Habitrack.Api.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Users.FindAsync([id], ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        return await _db.Users
            .AnyAsync(u => u.Email == email, ct);
    }

    public void Add(User user) => _db.Users.Add(user);
}
