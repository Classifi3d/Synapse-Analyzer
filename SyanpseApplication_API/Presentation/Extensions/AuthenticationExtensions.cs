using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Presentation.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Validates the JWT issued by the SSO provider. Two modes are supported: an OIDC authority
    /// for real deployments, or a symmetric signing key so the pipeline can be exercised
    /// locally without standing up an identity provider.
    /// </summary>
    public static IServiceCollection AddSynapseAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authority = configuration["Jwt:Authority"];
        var audience = configuration["Jwt:Audience"];
        var signingKey = configuration["Jwt:SigningKey"];

        if (string.IsNullOrWhiteSpace(authority) && string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                "Configure either Jwt:Authority (SSO) or Jwt:SigningKey (local development).");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                if (!string.IsNullOrWhiteSpace(authority))
                {
                    options.Authority = authority;
                    options.Audience = audience;
                    options.RequireHttpsMetadata =
                        !authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = !string.IsNullOrWhiteSpace(configuration["Jwt:Issuer"]),
                        ValidIssuer = configuration["Jwt:Issuer"],
                        ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                        ValidAudience = audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(signingKey!)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
                }

                options.Events = new JwtBearerEvents
                {
                    // EventSource cannot set an Authorization header, so the SSE endpoint
                    // accepts the token as a query parameter instead.
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token) &&
                            context.Request.Path.StartsWithSegments("/api/analysis") &&
                            context.Request.Query.TryGetValue("access_token", out var token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
