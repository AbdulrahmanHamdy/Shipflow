using Microsoft.AspNetCore.Cors.Infrastructure;

namespace ShipFlow.API.Configuration;

public static class CorsSetup
{
    public const string PolicyName = "ShipFlowCors";

    /// <summary>
    /// Allows only the origins listed in <c>Cors:AllowedOrigins</c>. With no origins configured, cross-origin
    /// browser calls are refused. Credentials (cookies) are not allowed: the API uses bearer tokens.
    /// </summary>
    public static IServiceCollection AddShipFlowCors(this IServiceCollection services)
    {
        services.AddCors();
        services
            .AddOptions<CorsOptions>()
            .Configure<IConfiguration>((cors, configuration) =>
            {
                var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                cors.AddPolicy(PolicyName, policy =>
                {
                    if (origins.Length > 0)
                    {
                        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
                    }
                });
            });

        return services;
    }
}
