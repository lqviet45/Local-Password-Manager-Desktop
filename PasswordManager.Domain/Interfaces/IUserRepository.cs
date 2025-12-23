using PasswordManager.Domain.Entities;

namespace PasswordManager.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, 
        bool isTracked = false,
        CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, 
        bool isTracked = false,
        CancellationToken ct = default);
    Task<User> AddAsync(User user, CancellationToken ct = default);
    Task<User> UpdateAsync(User user, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
}