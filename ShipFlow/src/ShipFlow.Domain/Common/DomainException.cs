namespace ShipFlow.Domain.Common;

/// <summary>
/// A business rule or invariant was violated. <see cref="Code"/> is a stable, machine-readable
/// identifier the API layer can map to a response; <see cref="Exception.Message"/> is safe to show.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>Stable error codes used by <see cref="DomainException"/>.</summary>
public static class DomainErrorCodes
{
    public const string Validation = "validation";
    public const string InvalidState = "invalid_state";
    public const string InvalidTransition = "invalid_transition";
    public const string NotOwner = "not_owner";
    public const string CrossCompany = "cross_company";
    public const string CapacityExceeded = "capacity_exceeded";
    public const string Conflict = "assignment_conflict";
    public const string QuoteRule = "quote_rule";
    public const string NotFound = "not_found";
}
