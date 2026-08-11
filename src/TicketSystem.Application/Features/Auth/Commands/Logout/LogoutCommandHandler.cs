using MediatR;
using Microsoft.Extensions.Logging;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Security;

namespace TicketSystem.Application.Features.Auth.Commands.Logout;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(
        IUnitOfWork uow,
        IClock clock,
        ICurrentUser currentUser,
        ILogger<LogoutCommandHandler> logger)
    {
        _uow = uow;
        _clock = clock;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task Handle(LogoutCommand cmd, CancellationToken ct)
    {
        var hash = TokenHashing.Hash(cmd.RefreshToken);

        var token = await _uow.Repository<Domain.Users.RefreshToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        // C2 FIX (IDOR) — silently succeed unless the presented token both exists AND belongs
        // to the calling user. Without this ownership check, any authenticated user could
        // revoke any other user's session by presenting a stolen refresh token value.
        // Idempotent 204 either way (AC-3, no info leak about whether the token exists).
        if (token is not null
            && token.RevokedAt is null
            && _currentUser.UserId is Guid callerId
            && token.UserId == callerId)
        {
            token.RevokedAt = _clock.UtcNow;
            await _uow.SaveChangesAsync(ct);
        }
        else if (token is not null && _currentUser.UserId is Guid caller && token.UserId != caller)
        {
            _logger.LogWarning(
                "Logout attempted for a token that does not belong to the caller. Caller={CallerId}, TokenOwner={TokenOwner}",
                caller, token.UserId);
        }
    }
}
