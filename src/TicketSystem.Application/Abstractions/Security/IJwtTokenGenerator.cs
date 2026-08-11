using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Abstractions.Security;

public interface IJwtTokenGenerator
{
    (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(User user);
}
