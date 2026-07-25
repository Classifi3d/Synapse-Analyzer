using Domain.Entities;

namespace Domain.Interfaces;

public interface IAnalysisRepository
{
    Task<Analysis> AddAsync(Analysis analysis, CancellationToken cancellationToken = default);

    /// <summary>Returns the analysis only when it belongs to <paramref name="userId"/>; otherwise null.</summary>
    Task<Analysis?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Analysis>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Analysis analysis, CancellationToken cancellationToken = default);
}
