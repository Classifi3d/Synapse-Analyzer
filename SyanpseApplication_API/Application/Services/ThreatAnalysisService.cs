using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace Application.Services;

public class ThreatAnalysisService : IThreatAnalysisService
{
    private readonly IAnalysisRepository _analysisRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IZeekProcessor _zeekProcessor;
    private readonly ILocalLlmProvider _llmProvider;

    public ThreatAnalysisService(
        IAnalysisRepository analysisRepository,
        IFileStorageService fileStorageService,
        IZeekProcessor zeekProcessor,
        ILocalLlmProvider llmProvider)
    {
        _analysisRepository = analysisRepository;
        _fileStorageService = fileStorageService;
        _zeekProcessor = zeekProcessor;
        _llmProvider = llmProvider;
    }

    public async Task<PcapUploadResultDto> UploadPcapAsync(Guid userId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty or null.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".pcap" && extension != ".pcapng")
            throw new ArgumentException("Invalid file type. Only .pcap and .pcapng are allowed.");


        string storagePath;
        using (var stream = file.OpenReadStream())
        {
            storagePath = await _fileStorageService.UploadFileAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length);
        }

        var analysisResult = new ThreatAnalysisResult
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = file.FileName,
            FilePath = storagePath,
            FileSizeBytes = file.Length,
            UploadedAt = DateTime.UtcNow,
            Status = "Pending"
        };

        var savedEntity = await _analysisRepository.AddAsync(analysisResult);

        return new PcapUploadResultDto
        {
            AnalysisId = savedEntity.Id,
            FileName = savedEntity.FileName,
            Status = savedEntity.Status,
            UploadedAt = savedEntity.UploadedAt
        };
    }


    public async Task<ThreatAnalysisResultDto> ProcessAnalysisAsync(Guid userId, Guid analysisId, string prompt)
    {
        var analysisRecord = await _analysisRepository.GetByIdAsync(analysisId);
        if (analysisRecord == null || analysisRecord.UserId != userId)
            throw new UnauthorizedAccessException("Analysis record not found or access denied.");

        if (analysisRecord.Status == "Processing" || analysisRecord.Status == "Completed")
            throw new InvalidOperationException("Analysis is already processing or completed.");

        analysisRecord.Status = "Processing";
        await _analysisRepository.UpdateAsync(analysisRecord);

        var tempWorkspace = Path.Combine(Path.GetTempPath(), "PacketGuard", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempWorkspace);
        var localPcapPath = Path.Combine(tempWorkspace, "capture.pcap");

        try
        {
            await _fileStorageService.DownloadFileAsync(analysisRecord.FilePath, localPcapPath);

            var logDirectory = await _zeekProcessor.ProcessPcapAsync(localPcapPath, tempWorkspace);

            var aggregatedLogs = await AggregateZeekLogsAsync(logDirectory);

            var llmResponse = await _llmProvider.AnalyzeAsync(prompt, aggregatedLogs);

            analysisRecord.Status = "Completed";
            analysisRecord.AnalysisDetails = llmResponse.Details;
            analysisRecord.IsThreatDetected = llmResponse.IsThreat;
            await _analysisRepository.UpdateAsync(analysisRecord);

            return new ThreatAnalysisResultDto
            {
                Id = analysisRecord.Id,
                Details = analysisRecord.AnalysisDetails,
                IsThreatDetected = analysisRecord.IsThreatDetected
            };
        }
        catch (Exception ex)
        {
            analysisRecord.Status = "Failed";
            analysisRecord.AnalysisDetails = $"Error during processing: {ex.Message}";
            await _analysisRepository.UpdateAsync(analysisRecord);
            throw;
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, true);
            }
        }
    }

    private async Task<string> AggregateZeekLogsAsync(string logDirectory)
    {
        var sb = new StringBuilder();

        var connLogPath = Path.Combine(logDirectory, "conn.log");
        if (File.Exists(connLogPath))
        {
            sb.AppendLine("CONN.LOG");
            var connLines = await File.ReadAllLinesAsync(connLogPath);
            sb.AppendLine(string.Join("\n", connLines.Take(100))); 
        }

        var dnsLogPath = Path.Combine(logDirectory, "dns.log");
        if (File.Exists(dnsLogPath))
        {
            sb.AppendLine("\n");
            sb.AppendLine("DNS.LOG");
            var dnsLines = await File.ReadAllLinesAsync(dnsLogPath);
            sb.AppendLine(string.Join("\n", dnsLines.Take(100)));
        }

        return sb.ToString();
    }
}