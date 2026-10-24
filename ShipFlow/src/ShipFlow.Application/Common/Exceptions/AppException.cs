namespace ShipFlow.Application.Common.Exceptions;

/// <summary>
/// Base type for expected, client-facing application failures. <see cref="Code"/> is a stable machine-readable
/// identifier; <see cref="Exception.Message"/> is safe to show to the caller. The API maps each subtype to an
/// RFC 7807 response.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>The request payload failed validation. Carries per-field messages.</summary>
public sealed class ValidationFailedException : AppException
{
    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base("validation", "One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>Credentials were missing or wrong. Deliberately never says which part was wrong.</summary>
public sealed class AuthenticationFailedException : AppException
{
    public AuthenticationFailedException(string message = "Invalid email or password.")
        : base("authentication_failed", message)
    {
    }
}

/// <summary>The request conflicts with existing state (for example a duplicate email address).</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base("conflict", message)
    {
    }
}

/// <summary>
/// The resource does not exist <b>or the caller may not know it exists</b>. Used instead of 403 for other users'
/// shipments so ids cannot be probed.
/// </summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message = "The requested resource was not found.") : base("not_found", message)
    {
    }
}

/// <summary>The caller is known and may see the resource but is not allowed to perform this action.</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You are not allowed to perform this action.") : base("forbidden", message)
    {
    }
}

/// <summary>Another request changed the same data first (optimistic concurrency or a unique-rule race).</summary>
public sealed class ConcurrencyConflictException : AppException
{
    public ConcurrencyConflictException()
        : base("concurrency_conflict", "The resource was changed by another request. Reload it and try again.")
    {
    }
}
