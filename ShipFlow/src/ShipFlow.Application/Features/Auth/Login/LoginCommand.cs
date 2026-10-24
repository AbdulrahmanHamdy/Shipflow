using FluentValidation;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Validation;
using ShipFlow.Application.Features.Auth.Register;

namespace ShipFlow.Application.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // Only presence and size are checked here: telling a caller which password rule failed would help
        // an attacker, and the real decision is made against the stored hash.
        RuleFor(x => x.Email).NotEmpty().MaximumLength(RegisterCommandValidator.MaxEmailLength);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(ShipFlow.Application.Common.Security.PasswordPolicy.MaxLength);
    }
}

public sealed class LoginHandler
{
    private readonly IValidator<LoginCommand> _validator;
    private readonly IIdentityService _identity;
    private readonly ITokenService _tokens;

    public LoginHandler(IValidator<LoginCommand> validator, IIdentityService identity, ITokenService tokens)
    {
        _validator = validator;
        _identity = identity;
        _tokens = tokens;
    }

    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);

        var user = await _identity.ValidateCredentialsAsync(command.Email.Trim(), command.Password, cancellationToken)
            ?? throw new AuthenticationFailedException();

        var token = _tokens.CreateAccessToken(user);
        return new LoginResult(token.Value, "Bearer", token.ExpiresAtUtc, UserDto.From(user));
    }
}
