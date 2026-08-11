using System.Security.Cryptography;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Security;

namespace TicketSystem.Infrastructure.Security;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public (string RawToken, string Hash) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32); // 256 bits
        var raw = Convert.ToBase64String(bytes);
        var hash = TokenHashing.Hash(raw);              // one source of truth (shared with 2.4/2.5)
        return (raw, hash);
    }
}
