using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ShipFlow.API.Configuration;

public static class RateLimitingSetup
{
    /// <summary>Policy for credential endpoints (register, login): slows password guessing and signup abuse.</summary>
    public const string AuthPolicy = "auth";

    public const int DefaultAuthPermitLimit = 10;
    public const int DefaultAuthWindowSeconds = 60;

    /// <summary>
    /// Fixed-window limit per client IP. Limits are read from <c>RateLimiting:AuthPermitLimit</c> /
    /// <c>RateLimiting:AuthWindowSeconds</c> at first use. Behind a reverse proxy, forwarded headers must be
    /// configured or every client shares the proxy's address (deployment phase).
    /// </summary>
    public static IServiceCollection AddShipFlowRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AuthPolicy, httpContext =>
            {
                var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
                var permitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", DefaultAuthPermitLimit);
                var windowSeconds = configuration.GetValue("RateLimiting:AuthWindowSeconds", DefaultAuthWindowSeconds);
                var client = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    client,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests.",
                        Type = "https://tools.ietf.org/html/rfc6585#section-4"
                    },
                    cancellationToken);
            };
        });

        return services;
    }
}
