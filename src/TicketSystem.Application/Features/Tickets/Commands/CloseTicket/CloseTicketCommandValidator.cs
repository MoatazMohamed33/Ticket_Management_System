using FluentValidation;
using TicketSystem.Application.Common.Validators;

namespace TicketSystem.Application.Features.Tickets.Commands.CloseTicket;

public sealed class CloseTicketCommandValidator : AbstractValidator<CloseTicketCommand>
{
    public CloseTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.RowVersion).ValidBase64();
    }
}
