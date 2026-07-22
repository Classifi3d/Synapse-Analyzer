using Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace Application.Interfaces;

public interface IThreatAnalysisService
{
    Task<PcapUploadResultDto> UploadPcapAsync(Guid userId, IFormFile file);
    Task<ThreatAnalysisResultDto> ProcessAnalysisAsync(Guid userId, Guid analysisId, string prompt);
}
