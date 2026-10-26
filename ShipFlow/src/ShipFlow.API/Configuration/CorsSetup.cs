using Microsoft.AspNetCore.Cors.Infrastructure;

namespace ShipFlow.API.Configuration;

public static class CorsSetup
{
    public const string PolicyName = "ShipFlowCors";

    /// <summary>
    /// Allows origins listed in <c>Cors:AllowedOrigins</c>, plus any *.vercel.app deployment or localhost.
    /// Credentials (cookies) are not required: the API uses bearer tokens.
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
                    policy
                        .SetIsOriginAllowed(origin =>
                        {
                            if (string.IsNullOrWhiteSpace(origin))
                            {
                                return false;
                            }

                            if (origins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                            {
                                return true;
                            }

                            try
                            {
                                var uri = new Uri(origin);
                                return uri.Host.EndsWith("vercel.app", StringComparison.OrdinalIgnoreCase)
                                    || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                                    || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                                    || uri.Host.EndsWith("runasp.net", StringComparison.OrdinalIgnoreCase);
                            }
                            catch
                            {
                                return false;
                            }
                        })
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

        return services;
    }
}
