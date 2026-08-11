namespace TicketSystem.Application.Common.Exceptions;

/// <summary>
/// Thrown by handlers for business-rule failures that should map to HTTP 400.
/// Distinct from FluentValidation's ValidationException, which carries the `errors`
/// extension for field-level failures; BadRequestException is a single semantic error
/// (self-lockout, last-Admin removal, etc.) with only a Detail message.
/// </summary>
public sealed class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}
