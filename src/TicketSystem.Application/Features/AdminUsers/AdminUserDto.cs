using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.AdminUsers;

/// <summary>
/// Public projection of <see cref="User"/> for admin surfaces. Never includes PasswordHash,
/// EmailNormalized, or refresh tokens. Reused across all Story 3.x endpoints.
/// </summary>
public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt);
