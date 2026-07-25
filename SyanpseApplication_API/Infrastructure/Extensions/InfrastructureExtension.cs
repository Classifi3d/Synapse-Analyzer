using Amazon.Runtime;
using Amazon.S3;
using Application.Interfaces;
using Application.Options;
using Application.Services;
using Domain.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.ExternalServices;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Extensions;

public static class InfrastructureExtension
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddPersistence(configuration)
            .AddObjectStorage(configuration)
            .AddZeek(configuration)
            .AddOllama(configuration)
            .AddApplicationServices(configuration);

        return services;
    }

    private static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Missing connection string 'Postgres'.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IAnalysisRepository, AnalysisRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }

    private static IServiceCollection AddObjectStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MinioOptions>()
            .Bind(configuration.GetSection(MinioOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Endpoint), "MinIO:Endpoint is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.AccessKey), "MinIO:AccessKey is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SecretKey), "MinIO:SecretKey is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.BucketName), "MinIO:BucketName is required.")
            .Validate(
                o => o.PartSizeBytes >= 5 * 1024 * 1024,
                "MinIO:PartSizeBytes must be at least 5 MiB, the S3 minimum part size.")
            .ValidateOnStart();

        // Client used for control-plane calls the API makes itself.
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
            return CreateClient(options, options.Endpoint);
        });

        // Client used to sign urls the browser will call. Signing binds the host, so these
        // must be generated against the publicly reachable endpoint.
        services.AddKeyedSingleton<IAmazonS3>(S3ClientKeys.Public, (sp, _) =>
        {
            var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;

            var endpoint = string.IsNullOrWhiteSpace(options.PublicEndpoint)
                ? options.Endpoint
                : options.PublicEndpoint;

            return CreateClient(options, endpoint);
        });

        services.AddScoped<IFileStorageService, S3FileStorageService>();
        services.AddHostedService<BucketInitializer>();

        return services;
    }

    private static IServiceCollection AddZeek(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ZeekOptions>()
            .Bind(configuration.GetSection(ZeekOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseAddress), "Zeek:BaseAddress is required.")
            .ValidateOnStart();

        services.AddHttpClient<IZeekProcessor, ZeekProcessor>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ZeekOptions>>().Value;

            client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseAddress));
            client.Timeout = options.Timeout;
        });

        return services;
    }

    private static IServiceCollection AddOllama(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OllamaOptions>()
            .Bind(configuration.GetSection(OllamaOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseAddress), "Ollama:BaseAddress is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Model), "Ollama:Model is required.")
            .ValidateOnStart();

        services.AddHttpClient<IOllamaService, OllamaService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;

            client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseAddress));
            client.Timeout = options.Timeout;
        });

        return services;
    }

    private static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AnalysisOptions>(configuration.GetSection(AnalysisOptions.SectionName));

        services.AddSingleton<IAnalysisPromptBuilder, AnalysisPromptBuilder>();
        services.AddScoped<IThreatAnalysisService, ThreatAnalysisService>();

        return services;
    }

    private static AmazonS3Client CreateClient(MinioOptions options, string endpoint)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,

            // MinIO addresses buckets as /bucket/key rather than bucket.host, which is what
            // the AWS default (virtual-hosted style) would produce.
            ForcePathStyle = true,

            AuthenticationRegion = options.Region
        };

        return new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            config);
    }

    private static string EnsureTrailingSlash(string address) =>
        address.EndsWith('/') ? address : address + "/";
}
