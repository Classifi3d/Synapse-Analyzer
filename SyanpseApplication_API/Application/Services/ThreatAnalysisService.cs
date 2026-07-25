using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Models;
using Application.Options;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

/// <summary>
/// Orchestrates the capture pipeline. This service coordinates storage, Zeek and the LLM but
/// never touches capture bytes: uploads go straight from the browser to MinIO, and Zeek pulls
/// the object itself.
/// </summary>
public class ThreatAnalysisService : IThreatAnalysisService
{
    private readonly IAnalysisRepository _analysisRepository;
    private readonly IUserRepository _userRepository;
    private readonly IFileStorageService _storage;
    private readonly IZeekProcessor _zeek;
    private readonly IOllamaService _ollama;
    private readonly IAnalysisPromptBuilder _promptBuilder;
    private readonly AnalysisOptions _options;
    private readonly ILogger<ThreatAnalysisService> _logger;

    private static readonly JsonSerializerOptions ZeekCacheJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ThreatAnalysisService(
        IAnalysisRepository analysisRepository,
        IUserRepository userRepository,
        IFileStorageService storage,
        IZeekProcessor zeek,
        IOllamaService ollama,
        IAnalysisPromptBuilder promptBuilder,
        IOptions<AnalysisOptions> options,
        ILogger<ThreatAnalysisService> logger)
    {
        _analysisRepository = analysisRepository;
        _userRepository = userRepository;
        _storage = storage;
        _zeek = zeek;
        _ollama = ollama;
        _promptBuilder = promptBuilder;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InitiateUploadResponseDto> InitiateUploadAsync(
        Guid userId,
        InitiateUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateUploadRequest(request);

        // The identity provider owns the user record; provision it locally on first sight so
        // the analysis foreign key resolves.
        await _userRepository.EnsureAsync(userId, string.Empty, string.Empty, cancellationToken);

        var analysisId = Guid.NewGuid();
        var objectKey = BuildObjectKey(userId, analysisId, request.FileName);
        var contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/vnd.tcpdump.pcap"
            : request.ContentType;

        var partCount = (int)Math.Ceiling((double)request.FileSize / _storage.PartSizeBytes);
        partCount = Math.Max(partCount, 1);

        if (partCount > 10_000)
        {
            throw new AnalysisValidationException(
                "The file requires more than the 10,000 parts S3 allows. Increase MinIO:PartSizeBytes.");
        }

        var session = await _storage.InitiateMultipartUploadAsync(
            objectKey,
            contentType,
            partCount,
            cancellationToken);

        var analysis = new Analysis
        {
            Id = analysisId,
            UserId = userId,
            FileName = request.FileName,
            ContentType = contentType,
            FileSizeBytes = request.FileSize,
            BucketName = _storage.BucketName,
            ObjectKey = objectKey,
            UploadId = session.UploadId,
            Status = AnalysisStatus.AwaitingUpload,
            CreatedAt = DateTime.UtcNow
        };

        await _analysisRepository.AddAsync(analysis, cancellationToken);

        _logger.LogInformation(
            "Opened upload session {UploadId} for analysis {AnalysisId} ({PartCount} parts).",
            session.UploadId,
            analysisId,
            partCount);

        return new InitiateUploadResponseDto
        {
            AnalysisId = analysisId,
            UploadId = session.UploadId,
            BucketName = _storage.BucketName,
            ObjectKey = objectKey,
            PartSizeBytes = _storage.PartSizeBytes,
            ExpiresAtUtc = session.ExpiresAtUtc,
            Parts = session.Parts
                .Select(p => new PresignedUploadPartDto
                {
                    PartNumber = p.PartNumber,
                    UploadUrl = p.UploadUrl
                })
                .ToList()
        };
    }

    public async Task<AnalysisDto> CompleteUploadAsync(
        Guid userId,
        CompleteUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var analysis = await GetOwnedAnalysisAsync(request.AnalysisId, userId, cancellationToken);

        if (analysis.Status != AnalysisStatus.AwaitingUpload)
            return ToDto(analysis);

        if (string.IsNullOrEmpty(analysis.UploadId))
        {
            throw new InvalidAnalysisStateException(
                "This analysis has no open upload session.");
        }

        var parts = request.Parts
            .OrderBy(p => p.PartNumber)
            .Select(p => new CompletedPart(p.PartNumber, p.ETag))
            .ToList();

        long size;

        try
        {
            size = await _storage.CompleteMultipartUploadAsync(
                analysis.ObjectKey,
                analysis.UploadId,
                parts,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete upload for analysis {AnalysisId}.", analysis.Id);

            analysis.Status = AnalysisStatus.Failed;
            analysis.ErrorMessage = $"Upload could not be completed: {ex.Message}";
            await _analysisRepository.UpdateAsync(analysis, cancellationToken);

            throw new AnalysisPipelineException("Failed to complete the multipart upload.", ex);
        }

        // Confirm the object is actually readable before declaring the upload done.
        var storedSize = await _storage.GetObjectSizeAsync(analysis.ObjectKey, cancellationToken);

        if (storedSize is null)
        {
            analysis.Status = AnalysisStatus.Failed;
            analysis.ErrorMessage = "The uploaded object could not be found in storage.";
            await _analysisRepository.UpdateAsync(analysis, cancellationToken);

            throw new AnalysisPipelineException("The uploaded object could not be found in storage.");
        }

        analysis.FileSizeBytes = storedSize ?? size;
        analysis.UploadId = null;

        if (!string.IsNullOrWhiteSpace(request.Prompt))
            analysis.Prompt = request.Prompt.Trim();

        analysis.Status = AnalysisStatus.Uploaded;
        analysis.UploadedAt = DateTime.UtcNow;
        analysis.ErrorMessage = null;

        await _analysisRepository.UpdateAsync(analysis, cancellationToken);

        _logger.LogInformation(
            "Upload completed for analysis {AnalysisId} ({Size} bytes).",
            analysis.Id,
            analysis.FileSizeBytes);

        return ToDto(analysis);
    }

    public async Task<AnalysisDto> GetAnalysisAsync(
        Guid userId,
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        var analysis = await GetOwnedAnalysisAsync(analysisId, userId, cancellationToken);
        return ToDto(analysis);
    }

    public async Task<IReadOnlyList<AnalysisDto>> ListAnalysesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var analyses = await _analysisRepository.ListForUserAsync(userId, cancellationToken);

        return analyses.Select(a => ToDto(a, includeReport: false)).ToList();
    }

    public async IAsyncEnumerable<AnalysisStreamEvent> StreamAnalysisAsync(
        Guid userId,
        Guid analysisId,
        string? prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var analysis = await GetOwnedAnalysisAsync(analysisId, userId, cancellationToken);

        if (analysis.Status == AnalysisStatus.AwaitingUpload)
        {
            throw new InvalidAnalysisStateException(
                "The capture has not finished uploading yet.");
        }

        // ---- Stage 1: Zeek -------------------------------------------------
        // Cached so re-running the report against a different prompt does not re-process
        // the capture.
        var zeekResult = DeserializeCachedZeek(analysis);

        if (zeekResult is null)
        {
            yield return AnalysisStreamEvent.Status("zeek", "Analyzing capture with Zeek...");

            analysis.Status = AnalysisStatus.Analyzing;
            analysis.ErrorMessage = null;
            await _analysisRepository.UpdateAsync(analysis, cancellationToken);

            string? zeekFailure = null;

            try
            {
                zeekResult = await RunZeekAsync(analysis, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Zeek analysis failed for {AnalysisId}.", analysis.Id);
                zeekFailure = ex.Message;
            }

            if (zeekFailure is not null || zeekResult is null)
            {
                var message = zeekFailure ?? "The Zeek service returned no result.";
                await MarkFailedAsync(analysis, message, cancellationToken);

                yield return AnalysisStreamEvent.Error(message);
                yield break;
            }

            analysis.ZeekResultJson = JsonSerializer.Serialize(zeekResult, ZeekCacheJson);
            analysis.AnalyzedAt = DateTime.UtcNow;
            await _analysisRepository.UpdateAsync(analysis, cancellationToken);
        }

        yield return AnalysisStreamEvent.Summary(zeekResult.Summary);

        // ---- Stage 2: prompt construction ----------------------------------
        // The request the analyst submitted with the upload is the default; a prompt passed
        // to the stream overrides it, which is what makes re-running against the cached Zeek
        // output with a new question possible.
        var analystRequest = string.IsNullOrWhiteSpace(prompt) ? analysis.Prompt : prompt;

        if (!string.IsNullOrWhiteSpace(prompt) && prompt.Trim() != analysis.Prompt)
        {
            analysis.Prompt = prompt.Trim();
        }

        var promptText = _promptBuilder.Build(analysis.FileName, analystRequest, zeekResult);

        analysis.Status = AnalysisStatus.Reporting;
        await _analysisRepository.UpdateAsync(analysis, cancellationToken);

        yield return AnalysisStreamEvent.Status("reporting", "Generating the analysis report...");

        // ---- Stage 3: LLM generation ---------------------------------------
        var report = new StringBuilder();
        string? streamFailure = null;

        await using (var tokens = _ollama.StreamAsync(promptText, cancellationToken)
                         .GetAsyncEnumerator(cancellationToken))
        {
            while (true)
            {
                string? token;

                try
                {
                    if (!await tokens.MoveNextAsync())
                        break;

                    token = tokens.Current;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Report generation failed for {AnalysisId}.", analysis.Id);
                    streamFailure = ex.Message;
                    break;
                }

                report.Append(token);
                yield return AnalysisStreamEvent.Token(token);
            }
        }

        if (streamFailure is not null)
        {
            await MarkFailedAsync(analysis, streamFailure, cancellationToken);

            yield return AnalysisStreamEvent.Error(streamFailure);
            yield break;
        }

        // ---- Stage 4: persist ----------------------------------------------
        var (isThreat, verdict) = VerdictParser.Parse(report.ToString());

        analysis.Report = report.ToString();
        analysis.IsThreatDetected = isThreat;
        analysis.Verdict = verdict;
        analysis.Status = AnalysisStatus.Completed;
        analysis.CompletedAt = DateTime.UtcNow;
        analysis.ErrorMessage = null;

        await _analysisRepository.UpdateAsync(analysis, cancellationToken);

        _logger.LogInformation(
            "Analysis {AnalysisId} completed. Threat detected: {IsThreat}.",
            analysis.Id,
            isThreat);

        yield return AnalysisStreamEvent.Done(analysis.Id, isThreat, verdict);
    }

    private async Task<ZeekAnalysisResultDto> RunZeekAsync(
        Analysis analysis,
        CancellationToken cancellationToken)
    {
        var downloadUrl = await _storage.CreatePresignedDownloadUrlAsync(
            analysis.ObjectKey,
            _options.DownloadUrlLifetime,
            cancellationToken);

        var result = await _zeek.AnalyzeAsync(
            analysis.Id,
            analysis.BucketName,
            analysis.ObjectKey,
            downloadUrl,
            cancellationToken);

        if (!result.Success)
        {
            throw new AnalysisPipelineException(
                result.Error ?? "The Zeek service reported a failure.");
        }

        return result;
    }

    private ZeekAnalysisResultDto? DeserializeCachedZeek(Analysis analysis)
    {
        if (string.IsNullOrWhiteSpace(analysis.ZeekResultJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ZeekAnalysisResultDto>(
                analysis.ZeekResultJson,
                ZeekCacheJson);
        }
        catch (JsonException ex)
        {
            // A cache miss is recoverable - fall through and re-run Zeek.
            _logger.LogWarning(
                ex,
                "Cached Zeek result for {AnalysisId} could not be read; re-running Zeek.",
                analysis.Id);

            return null;
        }
    }

    private async Task MarkFailedAsync(
        Analysis analysis,
        string message,
        CancellationToken cancellationToken)
    {
        analysis.Status = AnalysisStatus.Failed;
        analysis.ErrorMessage = message;

        // The stream may already be cancelled; failure state must still be recorded.
        await _analysisRepository.UpdateAsync(analysis, CancellationToken.None);
    }

    private async Task<Analysis> GetOwnedAnalysisAsync(
        Guid analysisId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _analysisRepository.GetForUserAsync(analysisId, userId, cancellationToken)
               ?? throw new AnalysisNotFoundException(analysisId);
    }

    private void ValidateUploadRequest(InitiateUploadRequestDto request)
    {
        if (request.FileSize <= 0)
            throw new AnalysisValidationException("File size must be greater than zero.");

        if (request.FileSize > _options.MaxFileSizeBytes)
        {
            throw new AnalysisValidationException(
                $"File exceeds the maximum size of {_options.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();

        if (!_options.AllowedExtensions.Contains(extension))
        {
            throw new AnalysisValidationException(
                $"Unsupported file type '{extension}'. Allowed: {string.Join(", ", _options.AllowedExtensions)}.");
        }
    }

    /// <summary>
    /// Namespaces the object by user and analysis so keys are unguessable and collisions are
    /// impossible, while the original name stays visible for operators browsing the bucket.
    /// </summary>
    private static string BuildObjectKey(Guid userId, Guid analysisId, string fileName)
    {
        var safeName = new string(Path.GetFileName(fileName)
            .Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_')
            .ToArray());

        if (safeName.Length > 100)
            safeName = safeName[^100..];

        return $"captures/{userId}/{analysisId}/{safeName}";
    }

    private static AnalysisDto ToDto(Analysis analysis, bool includeReport = true)
    {
        ZeekSummaryDto? summary = null;

        if (!string.IsNullOrWhiteSpace(analysis.ZeekResultJson))
        {
            try
            {
                summary = JsonSerializer
                    .Deserialize<ZeekAnalysisResultDto>(analysis.ZeekResultJson, ZeekCacheJson)
                    ?.Summary;
            }
            catch (JsonException)
            {
                summary = null;
            }
        }

        return new AnalysisDto
        {
            AnalysisId = analysis.Id,
            FileName = analysis.FileName,
            FileSizeBytes = analysis.FileSizeBytes,
            Status = analysis.Status.ToString(),
            CreatedAt = analysis.CreatedAt,
            UploadedAt = analysis.UploadedAt,
            CompletedAt = analysis.CompletedAt,
            Prompt = analysis.Prompt,
            IsThreatDetected = analysis.IsThreatDetected,
            Verdict = analysis.Verdict,
            Report = includeReport ? analysis.Report : null,
            ErrorMessage = analysis.ErrorMessage,
            ZeekSummary = summary
        };
    }
}
