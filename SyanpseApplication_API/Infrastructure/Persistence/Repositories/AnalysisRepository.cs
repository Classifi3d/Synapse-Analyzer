using Domain.Entities;
using Domain.Interfaces;

namespace Infrastructure.Persistence.Repositories;

public class AnalysisRepository : IAnalysisRepository
{
    private readonly AppDbContext _context;

    public AnalysisRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ThreatAnalysisResult> AddAsync(ThreatAnalysisResult result)
    {
        await _context.ThreatAnalysisResults.AddAsync(result);
        await _context.SaveChangesAsync(); 
        return result;
    }

    public async Task<ThreatAnalysisResult?> GetByIdAsync(Guid id)
    {
        return await _context.ThreatAnalysisResults.FindAsync(id);
    }
}
