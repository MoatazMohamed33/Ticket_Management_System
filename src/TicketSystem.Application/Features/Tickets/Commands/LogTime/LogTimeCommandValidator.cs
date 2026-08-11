using FluentValidation;

namespace TicketSystem.Application.Features.Tickets.Commands.LogTime;

public sealed class LogTimeCommandValidator : AbstractValidator<LogTimeCommand>
{
    public LogTimeCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();

        // "Today or in the past" per server UTC clock. Timezone edge documented in story dev notes.
        RuleFor(x => x.WorkedOn)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Work date cannot be in the future.");

        RuleFor(x => x.DurationMinutes)
            .GreaterThan(0)
            .LessThanOrEqualTo(24 * 60)
            .WithMessage("Duration must be between 1 and 1440 minutes.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .Must(s => !string.IsNullOrWhiteSpace(s)).WithMessage("Description cannot be empty.")
            .MaximumLength(1000);
    }
}
