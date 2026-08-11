using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.Auth.Commands.Login;

/// <summary>
/// Public projection of <see cref="User"/>. Never includes the password hash.
/// Reused by Story 2.2 (register response) and Story 2.6 (profile probe).
/// </summary>
public sealed record UserDto(Guid Id, string Email, string DisplayName, UserRole Role);
