using MediatR;

namespace TicketSystem.Application.Features.AdminUsers.Commands.SetUserActive;

/// <summary>
/// Deactivate (IsActive=false) or reactivate (IsActive=true) a user.
/// Returns nothing — the controller responds with 204 No Content.
/// </summary>
public sealed record SetUserActiveCommand(Guid UserId, bool IsActive) : IRequest;
