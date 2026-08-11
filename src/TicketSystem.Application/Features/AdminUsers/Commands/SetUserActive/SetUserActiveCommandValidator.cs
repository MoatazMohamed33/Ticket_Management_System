using FluentValidation;

namespace TicketSystem.Application.Features.AdminUsers.Commands.SetUserActive;

public sealed class SetUserActiveCommandValidator : AbstractValidator<SetUserActiveCommand>
{
    public SetUserActiveCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
