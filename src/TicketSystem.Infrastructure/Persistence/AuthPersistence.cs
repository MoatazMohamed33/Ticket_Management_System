using Microsoft.EntityFrameworkCore;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Domain.Users;

namespace TicketSystem.Infrastructure.Persistence;

public sealed class AuthPersistence : IAuthPersistence
{
    private readonly AppDbContext _db;
    public AuthPersistence(AppDbContext db) => _db = db;

    public Task<RefreshToken?> FindRefreshTokenWithUserAsync(string tokenHash, CancellationToken ct = default) =>
        _db.RefreshTokens
            .AsNoTracking()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public Task<int> TryRotateRefreshTokenAsync(
        string parentTokenHash,
        Guid newChildId,
        DateTimeOffset now,
        CancellationToken ct = default) =>
        _db.RefreshTokens
            .Where(t => t.TokenHash == parentTokenHash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.ReplacedByTokenId, newChildId), ct);

    public Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct = default) =>
        _db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

    public Task<int> RevokeAllUserTokensAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default) =>
        _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

    public Task<RefreshToken?> FindReplacementForAsync(Guid replacedTokenId, CancellationToken ct = default) =>
        _db.RefreshTokens
            .AsNoTracking()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == replacedTokenId, ct);
}
