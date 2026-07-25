using Domain.Entities;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User> EnsureAsync(
        Guid userId,
        string username,
        string email,
        CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is not null)
        {
            // Claims can change between logins; keep the local copy current.
            var changed = false;

            if (!string.IsNullOrWhiteSpace(username) && user.Username != username)
            {
                user.Username = username;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(email) && user.Email != email)
            {
                user.Email = email;
                changed = true;
            }

            if (changed)
                await context.SaveChangesAsync(cancellationToken);

            return user;
        }

        user = new User
        {
            Id = userId,
            Username = username,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two concurrent requests can race to provision the same identity; the loser
            // just reloads the row the winner inserted.
            context.Entry(user).State = EntityState.Detached;

            var existing = await context.Users
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (existing is null)
                throw;

            user = existing;
        }

        return user;
    }
}
