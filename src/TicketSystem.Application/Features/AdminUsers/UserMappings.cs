using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.AdminUsers;

/// <summary>Single mapping source of truth for admin-surface User → DTO projections.</summary>
public static class UserMappings
{
    public static AdminUserDto ToAdminUserDto(this User u) =>
        new(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.CreatedAt);
}
