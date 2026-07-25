using System.Diagnostics;
using System.Text.Json;
using Amazon.S3;
using Application.DTOs.Diagnostics;
using Application.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices;

/// <summary>
/// Probes each dependency directly rather than through its typed client, so a probe reports
/// reachability without being affected by the long timeouts the real calls need.
/// </summary>
public class ServiceHealthProbe(
    [FromKeyedServices(S3ClientKeys.Probe)] IAmazonS3 s3,
    IHttpClientFactory httpClientFactory,
    AppDbContext dbContext,
    IOptions<MinioOptions> minioOptions,
    IOptions<ZeekOptions> zeekOptions,
    IOptions<OllamaOptions> ollamaOptions) : IServiceHealthProbe
{
    public const string ProbeClientName = "diagnostics-probe";

    private readonly MinioOptions _minio = minioOptions.Value;
    private readonly ZeekOptions _zeek = zeekOptions.Value;
    private readonly OllamaOptions _ollama = ollamaOptions.Value;

    public Task<ComponentHealthDto> CheckMinioAsync(CancellationToken cancellationToken = default) =>
        MeasureAsync("minio", async () =>
        {
            var buckets = await s3.ListBucketsAsync(cancellationToken);
            var names = (buckets.Buckets ?? []).Select(b => b.BucketName).ToList();
            var captureBucketExists = names.Contains(_minio.BucketName);

            return (
                captureBucketExists,
                captureBucketExists
                    ? $"Reachable; bucket '{_minio.BucketName}' present."
                    : $"Reachable, but bucket '{_minio.BucketName}' is missing.",
                (object)new
                {
                    endpoint = _minio.Endpoint,
                    publicEndpoint = _minio.PublicEndpoint ?? _minio.Endpoint,
                    captureBucket = _minio.BucketName,
                    captureBucketExists,
                    buckets = names
                });
        });

    public Task<ComponentHealthDto> CheckZeekAsync(CancellationToken cancellationToken = default) =>
        MeasureAsync("zeek", async () =>
        {
            using var client = CreateProbeClient();

            using var response = await client.GetAsync(
                CombineUrl(_zeek.BaseAddress, "health"),
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // The Zeek service reports "degraded" when the binary is missing, which is a
            // usable response but not a healthy one.
            var payload = TryParse(body);
            var reportedStatus = payload is JsonElement element &&
                                 element.ValueKind == JsonValueKind.Object &&
                                 element.TryGetProperty("status", out var status)
                ? status.GetString()
                : null;

            var healthy = response.IsSuccessStatusCode && reportedStatus != "degraded";

            return (
                healthy,
                healthy
                    ? "Reachable; Zeek binary available."
                    : $"Responded {(int)response.StatusCode} with status '{reportedStatus ?? "unknown"}'.",
                (object)new { baseAddress = _zeek.BaseAddress, response = payload ?? body });
        });

    public Task<ComponentHealthDto> CheckOllamaAsync(CancellationToken cancellationToken = default) =>
        MeasureAsync("ollama", async () =>
        {
            using var client = CreateProbeClient();

            using var response = await client.GetAsync(
                CombineUrl(_ollama.BaseAddress, "api/tags"),
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var models = new List<string>();

            if (TryParse(body) is JsonElement root &&
                root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("models", out var modelArray) &&
                modelArray.ValueKind == JsonValueKind.Array)
            {
                models.AddRange(modelArray
                    .EnumerateArray()
                    .Select(m => m.TryGetProperty("name", out var name) ? name.GetString() : null)
                    .Where(name => name is not null)
                    .Select(name => name!));
            }

            // Ollama answers before a model is pulled, so a reachable server with the wrong
            // model set is still a failure for our purposes.
            var modelPresent = models.Any(m =>
                m == _ollama.Model ||
                m.StartsWith(_ollama.Model + ":", StringComparison.OrdinalIgnoreCase));

            return (
                modelPresent,
                modelPresent
                    ? $"Reachable; model '{_ollama.Model}' is pulled."
                    : $"Reachable, but model '{_ollama.Model}' is not pulled. " +
                      $"Run: docker compose exec ollama ollama pull {_ollama.Model}",
                (object)new
                {
                    baseAddress = _ollama.BaseAddress,
                    configuredModel = _ollama.Model,
                    modelPresent,
                    availableModels = models
                });
        });

    public Task<ComponentHealthDto> CheckPostgresAsync(CancellationToken cancellationToken = default) =>
        MeasureAsync("postgres", async () =>
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
                return (false, "Could not connect.", (object)new { pendingMigrations = Array.Empty<string>() });

            var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken))
                .ToList();

            return (
                pending.Count == 0,
                pending.Count == 0
                    ? "Connected; schema up to date."
                    : $"Connected, but {pending.Count} migration(s) have not been applied.",
                (object)new { pendingMigrations = pending });
        });

    private static async Task<ComponentHealthDto> MeasureAsync(
        string component,
        Func<Task<(bool Healthy, string Status, object? Details)>> probe)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var (healthy, status, details) = await probe();
            stopwatch.Stop();

            return new ComponentHealthDto
            {
                Component = component,
                Healthy = healthy,
                Status = status,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Details = details
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new ComponentHealthDto
            {
                Component = component,
                Healthy = false,
                Status = "Unreachable.",
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private HttpClient CreateProbeClient() => httpClientFactory.CreateClient(ProbeClientName);

    private static string CombineUrl(string baseAddress, string path) =>
        $"{baseAddress.TrimEnd('/')}/{path.TrimStart('/')}";

    private static object? TryParse(string body)
    {
        try
        {
            return JsonDocument.Parse(body).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
