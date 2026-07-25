using System.Text;
using System.Text.Json;
using Application.DTOs;
using Microsoft.AspNetCore.Http.Features;

namespace Presentation.Infrastructure;

/// <summary>Writes SSE frames to the response and keeps the connection unbuffered.</summary>
public sealed class ServerSentEventWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpResponse _response;

    public ServerSentEventWriter(HttpResponse response)
    {
        _response = response;

        _response.Headers.ContentType = "text/event-stream";
        _response.Headers.CacheControl = "no-cache,no-store";
        _response.Headers.Connection = "keep-alive";

        // Stops a reverse proxy such as nginx from buffering the stream and defeating the
        // whole point of streaming tokens.
        _response.Headers["X-Accel-Buffering"] = "no";

        _response.HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
    }

    public async Task WriteAsync(AnalysisStreamEvent @event, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(@event.Data, Json);

        var frame = new StringBuilder()
            .Append("event: ").Append(@event.Event).Append('\n')
            .Append("data: ").Append(payload).Append("\n\n")
            .ToString();

        await _response.WriteAsync(frame, cancellationToken);
        await _response.Body.FlushAsync(cancellationToken);
    }

    /// <summary>Sent when the pipeline throws before or during streaming.</summary>
    public Task WriteErrorAsync(string message, CancellationToken cancellationToken) =>
        WriteAsync(AnalysisStreamEvent.Error(message), cancellationToken);
}
