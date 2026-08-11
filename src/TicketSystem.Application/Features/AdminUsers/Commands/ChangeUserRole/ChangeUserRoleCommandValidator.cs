using FluentValidation;

namespace TicketSystem.Application.Features.AdminUsers.Commands.ChangeUserRole;

public sealed class ChangeUserRoleCommandValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        // Belt & braces — JsonStringEnumConverter rejects unknown strings at the JSON layer,
        // but if a raw int slips through (e.g., different serializer setup) IsInEnum catches it.
        RuleFor(x => x.Role).IsInEnum();
    }
}
