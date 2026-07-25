using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly IThreatAnalysisService _analysisService;

    public AnalysisController(IThreatAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    [HttpPost("upload/initiate")]
    public async Task<ActionResult<InitiateUploadResponseDto>> InitiateUpload(
        [FromBody] InitiateUploadRequestDto request)
    {
        var userId = GetUserId();

        var result = await _analysisService.InitiateUploadAsync(
            userId,
            request);

        return Ok(result);
    }

    [HttpPost("upload/complete")]
    public async Task<IActionResult> CompleteUpload(
        [FromBody] CompleteUploadRequestDto request)
    {
        var userId = GetUserId();

        await _analysisService.CompleteUploadAsync(userId, request);

        return NoContent();
    }

    //[HttpPost("{analysisId:guid}/process")]
    //public async Task<IActionResult> ProcessAnalysis(
    //    Guid analysisId,
    //    [FromBody] AnalyzePromptRequestDto request)
    //{
    //    var userId = GetUserId();

    //    var result = await _analysisService.ProcessAnalysisAsync(
    //        userId,
    //        analysisId,
    //        request.Prompt);

    //    return Ok(result);
    //}

    [HttpGet("{analysisId:guid}/stream")]
    public async Task StreamAnalysis(Guid analysisId)
    {
        var userId = GetUserId();

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");

        await foreach (var chunk in _analysisService.StreamAnalysisAsync(userId, analysisId))
        {
            await Response.WriteAsync($"data: {chunk}\n\n");
            await Response.Body.FlushAsync();
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException();

        return userId;
    }
}