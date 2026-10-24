using Microsoft.AspNetCore.Mvc;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Domain.Common;

namespace ShipFlow.API.Middleware;

/// <summary>How an exception is reported to the client, and how loudly it is logged.</summary>
public sealed record MappedProblem(ProblemDetails Problem, bool IsExpected);

/// <summary>
/// Maps application and domain exceptions to RFC 7807 problems. Messages from <see cref="AppException"/> and
/// <see cref="DomainException"/> are written to be shown to callers; any other exception becomes a generic 500
/// that never reveals its message or stack trace.
/// </summary>
public static class ProblemMapper
{
    public static MappedProblem Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        switch (exception)
        {
            case ValidationFailedException validation:
                var details = new ValidationProblemDetails(validation.Errors.ToDictionary(p => p.Key, p => p.Value))
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = validation.Message,
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                };
                details.Extensions["code"] = validation.Code;
                return new MappedProblem(details, IsExpected: true);

            case AuthenticationFailedException auth:
                return Expected(StatusCodes.Status401Unauthorized, "Authentication failed.", auth.Message, auth.Code);

            case NotFoundException notFound:
                return Expected(StatusCodes.Status404NotFound, "Resource not found.", notFound.Message, notFound.Code);

            case ForbiddenException forbidden:
                return Expected(StatusCodes.Status403Forbidden, "Access denied.", forbidden.Message, forbidden.Code);

            case ConcurrencyConflictException concurrency:
                return Expected(StatusCodes.Status409Conflict, "Conflict.", concurrency.Message, concurrency.Code);

            case ConflictException conflict:
                return Expected(StatusCodes.Status409Conflict, "Conflict.", conflict.Message, conflict.Code);

            case DomainException domain:
                return Expected(DomainStatus(domain.Code), DomainTitle(domain.Code), domain.Message, domain.Code);

            default:
                return new MappedProblem(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status500InternalServerError,
                        Title = "An unexpected error occurred.",
                        Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
                    },
                    IsExpected: false);
        }
    }

    public static int DomainStatus(string code) => code switch
    {
        DomainErrorCodes.Validation => StatusCodes.Status400BadRequest,
        DomainErrorCodes.NotFound => StatusCodes.Status404NotFound,
        DomainErrorCodes.NotOwner => StatusCodes.Status403Forbidden,
        DomainErrorCodes.CrossCompany => StatusCodes.Status403Forbidden,
        DomainErrorCodes.CapacityExceeded => StatusCodes.Status422UnprocessableEntity,
        DomainErrorCodes.InvalidState => StatusCodes.Status409Conflict,
        DomainErrorCodes.InvalidTransition => StatusCodes.Status409Conflict,
        DomainErrorCodes.QuoteRule => StatusCodes.Status409Conflict,
        DomainErrorCodes.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };

    private static string DomainTitle(string code) => code switch
    {
        DomainErrorCodes.NotFound => "Resource not found.",
        DomainErrorCodes.NotOwner or DomainErrorCodes.CrossCompany => "Access denied.",
        DomainErrorCodes.CapacityExceeded => "Capacity exceeded.",
        DomainErrorCodes.Conflict => "Assignment conflict.",
        DomainErrorCodes.Validation => "Invalid request.",
        _ => "The request conflicts with the current state."
    };

    private static MappedProblem Expected(int status, string title, string detail, string code)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        return new MappedProblem(problem, IsExpected: true);
    }
}
