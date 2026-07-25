using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Infrastructure;

namespace Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AnalysisController(
    IThreatAnalysisService analysisService,
    ILogger<AnalysisController> logger) : ControllerBase
{
    /// <summary>
    /// Opens an upload session. Returns one presigned url per part; the client uploads the
    /// chunks straight to MinIO, so no capture data passes through this API.
    /// </summary>
    [HttpPost("upload/initiate")]
    [ProducesResponseType<InitiateUploadResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InitiateUploadResponseDto>> InitiateUpload(
        [FromBody] InitiateUploadRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await analysisService.InitiateUploadAsync(
            User.GetUserId(),
            request,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Assembles the uploaded parts. The client must echo back the ETag MinIO returned for
    /// each part.
    /// </summary>
    [HttpPost("upload/complete")]
    [ProducesResponseType<AnalysisDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AnalysisDto>> CompleteUpload(
        [FromBody] CompleteUploadRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await analysisService.CompleteUploadAsync(
            User.GetUserId(),
            request,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AnalysisDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AnalysisDto>>> List(
        CancellationToken cancellationToken)
    {
        return Ok(await analysisService.ListAnalysesAsync(User.GetUserId(), cancellationToken));
    }

    [HttpGet("{analysisId:guid}")]
    [ProducesResponseType<AnalysisDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalysisDto>> Get(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        return Ok(await analysisService.GetAnalysisAsync(
            User.GetUserId(),
            analysisId,
            cancellationToken));
    }

    /// <summary>
    /// Runs the pipeline and streams the report as server-sent events. Communication is
    /// one-way once analysis begins, which is why this is SSE rather than a websocket.
    /// </summary>
    /// <remarks>
    /// Emits four event types: <c>status</c> (stage changes), <c>summary</c> (Zeek counters),
    /// <c>token</c> (generated text), and <c>done</c> or <c>error</c> as the final frame.
    /// </remarks>
    [HttpGet("{analysisId:guid}/stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task Stream(
        Guid analysisId,
        [FromQuery] string? prompt,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var writer = new ServerSentEventWriter(Response);

        try
        {
            await foreach (var @event in analysisService
                               .StreamAnalysisAsync(userId, analysisId, prompt, cancellationToken)
                               .WithCancellation(cancellationToken))
            {
                await writer.WriteAsync(@event, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client navigated away or closed the tab; nothing to report.
            logger.LogInformation("Client disconnected from analysis stream {AnalysisId}.", analysisId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Analysis stream {AnalysisId} failed.", analysisId);

            var message = ex switch
            {
                AnalysisNotFoundException or InvalidAnalysisStateException => ex.Message,
                _ => "The analysis failed unexpectedly."
            };

            await writer.WriteErrorAsync(message, CancellationToken.None);
        }
    }
}
