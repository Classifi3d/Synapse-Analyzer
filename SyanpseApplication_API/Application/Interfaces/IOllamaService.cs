namespace Application.Interfaces;

public interface IOllamaService
{
    /// <summary>
    /// Streams the model's response token by token. The prompt is fully constructed by the
    /// application layer - this service knows nothing about Zeek or packet captures.
    /// </summary>
    IAsyncEnumerable<string> StreamAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}
