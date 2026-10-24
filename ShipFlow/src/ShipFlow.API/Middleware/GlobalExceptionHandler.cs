using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ShipFlow.Application.Common.Exceptions;

namespace ShipFlow.API.Middleware;

/// <summary>
/// Central exception handling. Expected failures (validation, conflicts, domain rule violations) become precise
/// 4xx problems; anything else is logged and returned as a generic 500 with a trace id.
/// </summary>
public sealed partial class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client went away; nothing to report and nobody to report it to.
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var mapped = ProblemMapper.Map(exception);
        var problem = mapped.Problem;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (mapped.IsExpected)
        {
            LogExpected(exception.GetType().Name, httpContext.Request.Method, httpContext.Request.Path, problem.Status ?? 0);
        }
        else
        {
            LogUnhandled(exception, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        if (exception is AuthenticationFailedException)
        {
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";
        }

        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {ExceptionType} for {Method} {Path} -> {Status}")]
    private partial void LogExpected(string exceptionType, string method, PathString path, int status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private partial void LogUnhandled(Exception exception, string method, PathString path);
}
