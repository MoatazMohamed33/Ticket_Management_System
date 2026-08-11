using FluentValidation;

namespace TicketSystem.Application.Common.Validators;

/// <summary>
/// Shared FluentValidation helpers for rowVersion body fields. Used by any command that
/// applies optimistic concurrency (Story 4.5 close, Story 5.2 status, Epic 6 priority/assign).
/// </summary>
public static class RowVersionValidators
{
    public static IRuleBuilderOptions<T, string> ValidBase64<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Must(BeValidBase64)
            .WithMessage("rowVersion is malformed.");

    private static bool BeValidBase64(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        try { Convert.FromBase64String(s); return true; }
        catch (FormatException) { return false; }
    }
}
