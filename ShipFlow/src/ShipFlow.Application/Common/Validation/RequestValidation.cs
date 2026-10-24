using FluentValidation;
using ShipFlow.Application.Common.Exceptions;

namespace ShipFlow.Application.Common.Validation;

internal static class RequestValidation
{
    /// <summary>Validates <paramref name="request"/> and throws <see cref="ValidationFailedException"/> with per-field messages.</summary>
    public static async Task EnsureValidAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (result.IsValid)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        throw new ValidationFailedException(errors);
    }
}
