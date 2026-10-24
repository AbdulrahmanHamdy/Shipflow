using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ShipFlow.API.Configuration;

public static class RateLimitingSetup
{
    /// <summary>Policy for credential endpoints (register, login): slows password guessing and signup abuse.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>Per-user (sub claim) policy for GPS tracking submissions: prevents flooding.</summary>
    public const string TrackingPolicy = "tracking";

    public const int DefaultAuthPermitLimit = 10;
    public const int DefaultAuthWindowSeconds = 60;
    public const int DefaultTrackingPermitLimit = 60;
    public const int DefaultTrackingWindowSeconds = 60;

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

            // Per authenticated user (sub claim) for GPS tracking submissions.
            options.AddPolicy(TrackingPolicy, httpContext =>
            {
                var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
                var permitLimit = configuration.GetValue("RateLimiting:TrackingPermitLimit", DefaultTrackingPermitLimit);
                var windowSeconds = configuration.GetValue("RateLimiting:TrackingWindowSeconds", DefaultTrackingWindowSeconds);
                // Key on authenticated user; fall back to IP for unauthenticated requests (which the policy will also reject).
                var userId = httpContext.User.FindFirst("sub")?.Value
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    $"tracking:{userId}",
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
                response.ContentType = "application/problem+json";

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
                    options: null,
                    contentType: "application/problem+json",
                    cancellationToken: cancellationToken);
            };
        });

        return services;
    }
}
