using FluentValidation;

namespace TicketSystem.Application.Features.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        // Upper bound prevents CPU-DoS via giant "refresh token" values fed into SHA-256 + DB lookup.
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
    }
}
