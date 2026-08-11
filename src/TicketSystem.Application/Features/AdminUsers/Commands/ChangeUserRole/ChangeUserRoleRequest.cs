using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.AdminUsers.Commands.ChangeUserRole;

/// <summary>
/// Request body for PATCH /api/admin/users/{id}/role. Separated from
/// <see cref="ChangeUserRoleCommand"/> so the route parameter (id) is the sole source
/// of identity — no risk of a body `UserId` overriding the URL.
/// </summary>
public sealed record ChangeUserRoleRequest(UserRole Role);
