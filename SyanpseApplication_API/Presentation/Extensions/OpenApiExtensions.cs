using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Presentation.Extensions;

public static class OpenApiExtensions
{
    public const string DocumentName = "v1";

    public static IServiceCollection AddSynapseOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer(new DocumentInfoTransformer());
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<SecurityRequirementOperationTransformer>();
        });

        return services;
    }
}

internal sealed class DocumentInfoTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Synapse Analyzer API",
            Version = "v1",
            Description =
                """
                Orchestration API for AI-driven network threat analysis.

                Packet captures are never uploaded through this API. `upload/initiate` returns
                presigned URLs; the client PUTs each chunk directly to MinIO and echoes the
                per-part ETags back to `upload/complete`.

                `GET /api/analysis/{id}/stream` is a server-sent event stream, not a normal
                JSON endpoint - Swagger UI can call it, but the response is rendered as raw
                text rather than parsed. The events are `status`, `summary`, `token`, and a
                final `done` or `error`.

                Endpoints under `/api/diagnostics` exist only in the Development environment.
                """
        };

        return Task.CompletedTask;
    }
}

/// <summary>Declares the bearer scheme so Swagger UI shows an Authorize button.</summary>
internal sealed class BearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();

        if (schemes.All(s => s.Name != JwtBearerDefaults.AuthenticationScheme))
            return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] =
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "Paste the JWT only - Swagger adds the \"Bearer \" prefix. The token's " +
                    "sub claim must be a GUID; it becomes the user id every analysis is scoped to."
            };
    }
}

/// <summary>
/// Marks each operation as requiring the bearer scheme, except those that allow anonymous
/// access - so the padlock in Swagger UI reflects what the endpoint actually enforces.
/// </summary>
internal sealed class SecurityRequirementOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var allowsAnonymous = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAllowAnonymous>()
            .Any();

        if (allowsAnonymous)
            return Task.CompletedTask;

        // The reference must carry the host document, otherwise it serializes as an empty
        // object and Swagger UI shows no padlock.
        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(
                    JwtBearerDefaults.AuthenticationScheme,
                    context.Document)] = []
            }
        ];

        return Task.CompletedTask;
    }
}
