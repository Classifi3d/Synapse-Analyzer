using Domain.Entities;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class AnalysisRepository(AppDbContext context) : IAnalysisRepository
{
    public async Task<Analysis> AddAsync(
        Analysis analysis,
        CancellationToken cancellationToken = default)
    {
        await context.Analyses.AddAsync(analysis, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return analysis;
    }

    /// <summary>
    /// Ownership is part of the query rather than a check afterwards, so one user can never
    /// load another user's analysis.
    /// </summary>
    public Task<Analysis?> GetForUserAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return context.Analyses
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<Analysis>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await context.Analyses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        Analysis analysis,
        CancellationToken cancellationToken = default)
    {
        context.Analyses.Update(analysis);
        await context.SaveChangesAsync(cancellationToken);
    }
}
