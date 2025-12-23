using Microsoft.EntityFrameworkCore;
using PasswordManager.Domain.Entities;
using PasswordManager.Domain.Interfaces;

namespace PasswordManager.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation for user persistence.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly VaultDbContext _dbContext;

    public UserRepository(VaultDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<User?> GetByEmailAsync(string email, 
        bool isTracked = false,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (isTracked)
        {
            return await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email == email, ct);
        }
        
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<User?> GetByIdAsync(Guid id, 
        bool isTracked = false,
        CancellationToken ct = default)
    {
        if (isTracked)
        {
            return await _dbContext.Users.FindAsync([id], ct);
        }
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User> AddAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await _dbContext.Users.AddAsync(user, ct);
        await _dbContext.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User> UpdateAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(ct);
        return user;
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return await _dbContext.Users.AnyAsync(u => u.Email == email, ct);
    }
}

