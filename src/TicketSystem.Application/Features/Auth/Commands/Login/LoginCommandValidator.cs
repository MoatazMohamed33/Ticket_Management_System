using FluentValidation;

namespace TicketSystem.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    // Upper bounds on both fields prevent CPU-DoS via a giant password being fed to PBKDF2.
    // A megabyte password × 10 req/min (per IP under rate limit) pins a thread for hundreds
    // of ms each — trivially exhausts the thread pool.
    private const int MaxEmailLength = 320;    // RFC 5321 practical max
    private const int MaxPasswordLength = 256; // more than enough for any real passphrase

    public LoginCommandValidator()
    {
        // Deliberately minimal semantic validation — validation errors here must not
        // distinguish "malformed email" from "no such account", or we hand attackers an
        // email-enumeration oracle.
        RuleFor(x => x.Email).NotEmpty().MaximumLength(MaxEmailLength);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(MaxPasswordLength);
    }
}
