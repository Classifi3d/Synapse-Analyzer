using Application.DTOs;
using Application.DTOs.Diagnostics;
using Application.Interfaces;
using Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Presentation.Filters;

namespace Presentation.Controllers;

/// <summary>
/// Manual test surface for the pipeline's moving parts. Unauthenticated and routable only in
/// Development - <see cref="DevelopmentOnlyAttribute"/> turns every action into a 404
/// elsewhere.
/// </summary>
/// <remarks>
/// The storage endpoints here stream file bytes through the API, which the real upload path
/// never does. They exist to prove the bucket works, not as an alternative upload route.
/// </remarks>
[ApiController]
[AllowAnonymous]
[DevelopmentOnly]
[Route("api/[controller]")]
public class DiagnosticsController(
    IStorageDiagnostics storage,
    IServiceHealthProbe healthProbe,
    IFileStorageService fileStorage,
    IZeekProcessor zeek,
    IOptions<AnalysisOptions> analysisOptions,
    ILogger<DiagnosticsController> logger) : ControllerBase
{
    private readonly AnalysisOptions _analysisOptions = analysisOptions.Value;

    // ---- Storage round trip ------------------------------------------------

    /// <summary>Uploads a file straight to MinIO and returns where it landed.</summary>
    [HttpPost("storage/objects")]
    [RequestSizeLimit(2L * 1024 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024)]
    [ProducesResponseType<UploadedObjectDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UploadedObjectDto>> UploadObject(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file was supplied." });

        await using var stream = file.OpenReadStream();

        var result = await storage.UploadAsync(
            stream,
            file.FileName,
            string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Serves a stored object back, to confirm what went in comes out.</summary>
    [HttpGet("storage/objects/{**objectKey}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadObject(
        string objectKey,
        CancellationToken cancellationToken)
    {
        var result = await storage.DownloadAsync(objectKey, cancellationToken);

        if (result is null)
            return NotFound(new { error = $"'{objectKey}' was not found in the bucket." });

        var (content, contentType, _) = result.Value;

        return File(content, contentType, Path.GetFileName(objectKey));
    }

    [HttpGet("storage/objects")]
    [ProducesResponseType<IReadOnlyList<StoredObjectDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StoredObjectDto>>> ListObjects(
        [FromQuery] string? prefix,
        [FromQuery] int maxKeys = 100,
        CancellationToken cancellationToken = default)
    {
        var objects = await storage.ListAsync(
            prefix,
            Math.Clamp(maxKeys, 1, 1000),
            cancellationToken);

        return Ok(objects);
    }

    [HttpDelete("storage/objects/{**objectKey}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteObject(
        string objectKey,
        CancellationToken cancellationToken)
    {
        var deleted = await storage.DeleteAsync(objectKey, cancellationToken);

        return deleted
            ? NoContent()
            : NotFound(new { error = $"'{objectKey}' was not found in the bucket." });
    }

    // ---- Zeek ---------------------------------------------------------------

    /// <summary>
    /// Runs an object already in storage through Zeek and returns everything the service
    /// produced - full summary, every sampled log row, and timings. This is the uncapped view;
    /// the report pipeline trims the same data down before prompting.
    /// </summary>
    [HttpPost("zeek/analyze")]
    [ProducesResponseType<ZeekAnalysisResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ZeekAnalysisResultDto>> AnalyzeWithZeek(
        [FromBody] ZeekDiagnosticsRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ObjectKey))
            return BadRequest(new { error = "objectKey is required." });

        var size = await fileStorage.GetObjectSizeAsync(request.ObjectKey, cancellationToken);

        if (size is null)
        {
            return NotFound(new
            {
                error = $"'{request.ObjectKey}' was not found. Upload it first, or list " +
                        "/api/diagnostics/storage/objects to find the right key."
            });
        }

        // Same presigned-url handoff the real pipeline uses: the capture is fetched by the
        // Zeek service, never relayed through here.
        var downloadUrl = await fileStorage.CreatePresignedDownloadUrlAsync(
            request.ObjectKey,
            _analysisOptions.DownloadUrlLifetime,
            cancellationToken);

        logger.LogInformation(
            "Diagnostics Zeek run for {ObjectKey} ({Size} bytes).",
            request.ObjectKey,
            size);

        var result = await zeek.AnalyzeAsync(
            Guid.NewGuid(),
            request.BucketName ?? fileStorage.BucketName,
            request.ObjectKey,
            downloadUrl,
            cancellationToken);

        return Ok(result);
    }

    // ---- Health -------------------------------------------------------------

    /// <summary>Probes every dependency. Returns 503 when any of them is unhealthy.</summary>
    [HttpGet("health")]
    [ProducesResponseType<SystemHealthDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<SystemHealthDto>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SystemHealthDto>> Health(CancellationToken cancellationToken)
    {
        // Probed in parallel: four sequential timeouts would make a fully-down stack take
        // 20 seconds to report.
        var results = await Task.WhenAll(
            healthProbe.CheckPostgresAsync(cancellationToken),
            healthProbe.CheckMinioAsync(cancellationToken),
            healthProbe.CheckZeekAsync(cancellationToken),
            healthProbe.CheckOllamaAsync(cancellationToken));

        var report = new SystemHealthDto
        {
            Healthy = results.All(r => r.Healthy),
            CheckedAt = DateTime.UtcNow,
            Components = [.. results]
        };

        return report.Healthy
            ? Ok(report)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, report);
    }

    [HttpGet("health/minio")]
    public Task<ComponentHealthDto> MinioHealth(CancellationToken cancellationToken) =>
        healthProbe.CheckMinioAsync(cancellationToken);

    [HttpGet("health/zeek")]
    public Task<ComponentHealthDto> ZeekHealth(CancellationToken cancellationToken) =>
        healthProbe.CheckZeekAsync(cancellationToken);

    [HttpGet("health/ollama")]
    public Task<ComponentHealthDto> OllamaHealth(CancellationToken cancellationToken) =>
        healthProbe.CheckOllamaAsync(cancellationToken);

    [HttpGet("health/postgres")]
    public Task<ComponentHealthDto> PostgresHealth(CancellationToken cancellationToken) =>
        healthProbe.CheckPostgresAsync(cancellationToken);
}
