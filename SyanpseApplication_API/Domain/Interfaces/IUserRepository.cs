using Domain.Entities;

namespace Domain.Interfaces;

public interface IUserRepository
{
    /// <summary>
    /// Creates the local user record on first sight of an SSO identity. Users are never
    /// registered through this API - they arrive already authenticated, so the row is
    /// provisioned lazily from the token claims.
    /// </summary>
    Task<User> EnsureAsync(
        Guid userId,
        string username,
        string email,
        CancellationToken cancellationToken = default);
}
