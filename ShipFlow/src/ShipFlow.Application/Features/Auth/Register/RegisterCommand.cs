using FluentValidation;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Common.Validation;

namespace ShipFlow.Application.Features.Auth.Register;

/// <summary>
/// Public registration. There is intentionally no role field: every self-registered account is a Customer.
/// </summary>
public sealed record RegisterCommand(string Email, string Password, string FullName);

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public const int MaxEmailLength = 256;
    public const int MaxFullNameLength = 150;

    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(MaxEmailLength)
            .EmailAddress();

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(MaxFullNameLength);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(PasswordPolicy.MinLength)
            .MaximumLength(PasswordPolicy.MaxLength)
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
    }
}

public sealed class RegisterHandler
{
    private readonly IValidator<RegisterCommand> _validator;
    private readonly IIdentityService _identity;

    public RegisterHandler(IValidator<RegisterCommand> validator, IIdentityService identity)
    {
        _validator = validator;
        _identity = identity;
    }

    public async Task<UserDto> HandleAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);

        var user = await _identity.RegisterCustomerAsync(
            command.Email.Trim(),
            command.Password,
            command.FullName.Trim(),
            cancellationToken);

        return UserDto.From(user);
    }
}
