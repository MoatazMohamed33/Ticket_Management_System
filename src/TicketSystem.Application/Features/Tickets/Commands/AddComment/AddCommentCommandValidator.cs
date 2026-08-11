using FluentValidation;

namespace TicketSystem.Application.Features.Tickets.Commands.AddComment;

public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();

        RuleFor(x => x.Body)
            .NotEmpty()
            .Must(s => !string.IsNullOrWhiteSpace(s))
                .WithMessage("Body cannot be empty.")
            .MaximumLength(4000);
    }
}
