using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ShipFlow.API.Health;

/// <summary>Writes a minimal JSON health payload. Never includes exception details or internals.</summary>
public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            timestampUtc = DateTime.UtcNow
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
