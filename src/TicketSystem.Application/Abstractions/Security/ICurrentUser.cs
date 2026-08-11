namespace TicketSystem.Application.Abstractions.Security;

/// <summary>
/// Read-only view of the currently authenticated user for handlers that need to enforce
/// ownership checks. Implemented in Infrastructure/Api via HttpContextAccessor.
/// Returns null for anonymous requests.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
}
