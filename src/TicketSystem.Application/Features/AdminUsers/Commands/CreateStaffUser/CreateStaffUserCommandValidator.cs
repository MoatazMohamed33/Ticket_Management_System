using FluentValidation;
using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.AdminUsers.Commands.CreateStaffUser;

public sealed class CreateStaffUserCommandValidator : AbstractValidator<CreateStaffUserCommand>
{
    public CreateStaffUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Role)
            .Must(r => r == UserRole.Admin || r == UserRole.SupportAgent)
            .WithMessage("Customers self-register only; use /api/auth/register.");

        RuleFor(x => x.InitialPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(256)
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$")
                .WithMessage("Password must be at least 8 characters and include upper, lower, and digit.");
    }
}
