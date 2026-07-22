using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Ensures only authenticated users via SSO can hit this endpoint
    public class AnalysisController : ControllerBase
    {
        private readonly IThreatAnalysisService _threatAnalysisService;

        public AnalysisController(IThreatAnalysisService threatAnalysisService)
        {
            _threatAnalysisService = threatAnalysisService;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10L * 1024L * 1024L * 1024L)] // E.g., allow up to 10GB uploads via standard streams
        [RequestFormLimits(MultipartBodyLengthLimit = 10L * 1024L * 1024L * 1024L)]
        public async Task<IActionResult> UploadPcap(IFormFile file)
        {
            try
            {
                // Extract the UserId securely from the SSO JWT Token Claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                {
                    return Unauthorized("Invalid or missing user identity in token.");
                }

                // Pass to the application service
                var result = await _threatAnalysisService.UploadPcapAsync(userId, file);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("{analysisId:guid}/process")]
        public async Task<IActionResult> ProcessAnalysis(Guid analysisId, [FromBody] AnalyzePromptRequestDto request)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var result = await _threatAnalysisService.ProcessAnalysisAsync(userId, analysisId, request.Prompt);

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new UnauthorizedAccessException("Invalid or missing user identity in token.");
            }
            return userId;
        }
    }
}

