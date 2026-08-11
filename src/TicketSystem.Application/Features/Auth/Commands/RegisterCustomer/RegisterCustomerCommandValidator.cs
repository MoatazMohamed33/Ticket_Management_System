using FluentValidation;

namespace TicketSystem.Application.Features.Auth.Commands.RegisterCustomer;

public sealed class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(200);

        // Policy: 8+ chars including at least one lowercase, one uppercase, one digit.
        // Symbol NOT required — aligns with seed password "Passw0rd!" which satisfies this.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(256)
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$")
                .WithMessage("Password must be at least 8 characters and include upper, lower, and digit.");
    }
}
