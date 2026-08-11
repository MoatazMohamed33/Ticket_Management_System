using FluentValidation;
using TicketSystem.Application.Common.Validators;

namespace TicketSystem.Application.Features.Tickets.Commands.ChangeTicketStatus;

public sealed class ChangeTicketStatusCommandValidator : AbstractValidator<ChangeTicketStatusCommand>
{
    public ChangeTicketStatusCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.RowVersion).ValidBase64();
    }
}
