using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Application.Common.Security;
using TicketSystem.Application.Features.Auth.Commands.Login;

namespace TicketSystem.Application.Features.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    /// <summary>
    /// Grace window during which a re-presentation of a just-rotated parent token is treated
    /// as a legitimate client retry (network glitch, mobile handoff) instead of theft.
    /// Trade-off: recovers real retries at the cost of a small attacker replay window.
    /// </summary>
    private static readonly TimeSpan RetryGraceWindow = TimeSpan.FromSeconds(30);

    private readonly IUnitOfWork _uow;
    private readonly IAuthPersistence _authPersistence;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IRefreshTokenGenerator _refresh;
    private readonly IClock _clock;
    private readonly IOptions<JwtOptions> _opts;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUnitOfWork uow,
        IAuthPersistence authPersistence,
        IJwtTokenGenerator jwt,
        IRefreshTokenGenerator refresh,
        IClock clock,
        IOptions<JwtOptions> opts,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _uow = uow;
        _authPersistence = authPersistence;
        _jwt = jwt;
        _refresh = refresh;
        _clock = clock;
        _opts = opts;
        _logger = logger;
    }

    public async Task<AuthResponseDto> Handle(RefreshTokenCommand cmd, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var hex = TokenHashing.Hash(cmd.RefreshToken);

        var token = await _authPersistence.FindRefreshTokenWithUserAsync(hex, ct);
        if (token is null)
        {
            throw new InvalidCredentialsException("Invalid refresh token.");
        }

        if (token.ExpiresAt <= now)
        {
            throw new InvalidCredentialsException("Invalid refresh token.");
        }

        if (token.User is null || !token.User.IsActive || token.User.DeletedAt is not null)
        {
            throw new InvalidCredentialsException("Invalid refresh token.");
        }

        // Rotated-token replay path — could be theft OR a legitimate retry.
        if (token.RevokedAt is not null || token.ReplacedByTokenId is not null)
        {
            // C3 FIX (grace window): if the replacement child was issued very recently,
            // treat the replay as a benign client retry rather than nuking the family.
            if (token.ReplacedByTokenId is Guid replacementId)
            {
                var replacement = await _authPersistence.FindReplacementForAsync(replacementId, ct);
                if (replacement is not null
                    && replacement.RevokedAt is null
                    && (now - replacement.IssuedAt) <= RetryGraceWindow)
                {
                    _logger.LogInformation(
                        "Refresh retry within grace window — legitimate replay of parent {TokenId}. User {UserId}.",
                        token.Id, token.UserId);

                    // We can't return the raw child token (we only have its hash), so respond
                    // 401 to force the client to fall back to login rather than corrupt its
                    // token state. The important effect is: we did NOT invalidate the family.
                    throw new InvalidCredentialsException("Invalid refresh token.");
                }
            }

            var revoked = await _authPersistence.RevokeFamilyAsync(token.FamilyId, now, ct);
            _logger.LogWarning(
                "Potential token theft — rotated refresh replayed outside grace. Family {FamilyId} invalidated ({Revoked} tokens). User {UserId}.",
                token.FamilyId, revoked, token.UserId);
            throw new InvalidCredentialsException("Invalid refresh token.");
        }

        // C3 FIX (rotation race): atomic UPDATE with WHERE RevokedAt IS NULL. Two concurrent
        // refreshes on the same parent: only one wins (winnerRows=1). The loser (winnerRows=0)
        // returns 401 — the client's next attempt will hit the grace-window path and cleanly
        // retry, so no family invalidation on a legitimate double-tap.
        var newTokenId = Guid.NewGuid();
        var winnerRows = await _authPersistence.TryRotateRefreshTokenAsync(hex, newTokenId, now, ct);
        if (winnerRows == 0)
        {
            _logger.LogInformation(
                "Concurrent refresh lost the rotation race for token {TokenId}. User {UserId}.",
                token.Id, token.UserId);
            throw new InvalidCredentialsException("Invalid refresh token.");
        }

        // Insert the winner's new child. Parent update already happened in TryRotateRefreshTokenAsync.
        var (rawRefresh, refreshHash) = _refresh.Generate();
        var refreshExp = now.AddDays(_opts.Value.RefreshTokenDays);
        var newToken = new Domain.Users.RefreshToken
        {
            Id = newTokenId,
            UserId = token.UserId,
            TokenHash = refreshHash,
            FamilyId = token.FamilyId,
            IssuedAt = now,
            ExpiresAt = refreshExp,
        };
        await _uow.Repository<Domain.Users.RefreshToken>().AddAsync(newToken, ct);
        await _uow.SaveChangesAsync(ct);

        var (accessToken, accessExp) = _jwt.GenerateAccessToken(token.User);

        return new AuthResponseDto(
            accessToken,
            rawRefresh,
            accessExp,
            refreshExp,
            new UserDto(token.User.Id, token.User.Email, token.User.DisplayName, token.User.Role));
    }
}
