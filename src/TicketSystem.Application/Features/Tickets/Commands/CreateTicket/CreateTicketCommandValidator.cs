using FluentValidation;

namespace TicketSystem.Application.Features.Tickets.Commands.CreateTicket;

public sealed class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(8000);

        // JsonStringEnumConverter rejects unknown role strings in the JSON body; IsInEnum
        // is the belt-and-braces catch for raw-int payloads that bypass it.
        RuleFor(x => x.Priority).IsInEnum();
    }
}
