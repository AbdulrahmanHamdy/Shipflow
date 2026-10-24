namespace ShipFlow.Domain.Common;

/// <summary>Argument/invariant checks that throw <see cref="DomainException"/> with the validation code.</summary>
internal static class Guard
{
    public static Guid NotEmpty(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw Invalid($"{name} must not be empty.");
        }

        return value;
    }

    public static string NotBlank(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid($"{name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw Invalid($"{name} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    public static string? OptionalText(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NotBlank(value, name, maxLength);
    }

    public static DateTime Utc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw Invalid($"{name} must be a UTC timestamp.");
        }

        return value;
    }

    public static decimal Positive(decimal value, string name)
    {
        if (value <= 0m)
        {
            throw Invalid($"{name} must be greater than zero.");
        }

        return value;
    }

    public static T NotNull<T>(T? value, string name) where T : class
    {
        return value ?? throw Invalid($"{name} is required.");
    }

    public static DomainException Invalid(string message) =>
        new(DomainErrorCodes.Validation, message);
}
