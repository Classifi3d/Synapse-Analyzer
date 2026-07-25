using Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Infrastructure;

/// <summary>Maps application exceptions onto problem details responses.</summary>
public class AnalysisExceptionHandler(ILogger<AnalysisExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            AnalysisNotFoundException => (StatusCodes.Status404NotFound, "Analysis not found"),
            AnalysisValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
            InvalidAnalysisStateException => (StatusCodes.Status409Conflict, "Invalid state"),
            AnalysisPipelineException => (StatusCodes.Status502BadGateway, "Analysis pipeline failure"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception on {Path}.", httpContext.Request.Path);
        else
            logger.LogWarning("{Title} on {Path}: {Message}", title, httpContext.Request.Path, exception.Message);

        // The response has already started for streaming endpoints; the stream itself reports
        // the failure as an SSE error event.
        if (httpContext.Response.HasStarted)
            return true;

        httpContext.Response.StatusCode = status;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status >= StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred."
                    : exception.Message,
                Instance = httpContext.Request.Path
            },
            cancellationToken);

        return true;
    }
}
