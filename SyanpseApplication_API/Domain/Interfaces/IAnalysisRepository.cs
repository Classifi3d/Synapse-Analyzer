using Domain.Entities;

namespace Domain.Interfaces;

public interface IAnalysisRepository
{
    Task<ThreatAnalysisResult> AddAsync(ThreatAnalysisResult result);
    Task<ThreatAnalysisResult?> GetByIdAsync(Guid id);
}

