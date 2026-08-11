using FluentValidation;
using TicketSystem.Application.Common.Validators;

namespace TicketSystem.Application.Features.Tickets.Commands.ChangeTicketPriority;

public sealed class ChangeTicketPriorityCommandValidator : AbstractValidator<ChangeTicketPriorityCommand>
{
    public ChangeTicketPriorityCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.RowVersion).ValidBase64();
    }
}
