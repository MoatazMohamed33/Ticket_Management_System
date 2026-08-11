using FluentValidation;
using TicketSystem.Application.Common.Validators;

namespace TicketSystem.Application.Features.Tickets.Commands.AssignTicket;

public sealed class AssignTicketCommandValidator : AbstractValidator<AssignTicketCommand>
{
    public AssignTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.RowVersion).ValidBase64();
        // AssignedAgentId is nullable; existence/role validation happens in the handler
        // because it requires a DB round-trip (validators stay side-effect-free).
    }
}
