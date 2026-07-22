using Application.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.ExternalServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace Infrastructure.Extensions;

public static class InfrastructureExtension
{
    public static IServiceCollection AddMinioInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));

        var minioOptions = configuration.GetSection(MinioOptions.SectionName)
            .Get<MinioOptions>()
                ?? throw new InvalidOperationException("MinIO configuration section is missing.");

        services.AddMinio(configureClient => configureClient
            .WithEndpoint(minioOptions.Endpoint)
            .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey)
            .WithSSL(minioOptions.UseSSL));

        services.AddScoped<IFileStorageService, MinioFileStorageService>();

        return services;
    }

    public static IServiceCollection AddOllamaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        //services.AddHttpClient<IOllamaService, OllamaService>();

        return services;
    }
}
