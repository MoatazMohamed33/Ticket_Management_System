using MediatR;
using Microsoft.Extensions.Logging;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.AdminUsers.Commands.SetUserActive;

public sealed class SetUserActiveCommandHandler : IRequestHandler<SetUserActiveCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IAuthPersistence _authPersistence;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly ILogger<SetUserActiveCommandHandler> _logger;

    public SetUserActiveCommandHandler(
        IUnitOfWork uow,
        IAuthPersistence authPersistence,
        ICurrentUser currentUser,
        IClock clock,
        ILogger<SetUserActiveCommandHandler> logger)
    {
        _uow = uow;
        _authPersistence = authPersistence;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task Handle(SetUserActiveCommand cmd, CancellationToken ct)
    {
        // Phase 1: no-tracking read for the guard checks and idempotency short-circuit.
        // Loading tracked here would pin the entity in the change tracker even on paths
        // that don't mutate (idempotent-with-kick) — a footgun if the handler is extended.
        var snapshot = await _uow.Repository<User>()
            .FirstOrDefaultNoTrackingAsync(u => u.Id == cmd.UserId, ct);
        if (snapshot is null)
        {
            throw new NotFoundException("User not found.");
        }

        // Self-lockout guard (only for deactivate)
        if (!cmd.IsActive
            && _currentUser.UserId is Guid callerId
            && callerId == cmd.UserId)
        {
            throw new BadRequestException("You cannot deactivate your own account.");
        }

        // Last-Admin guard on deactivate — mirrors Story 3.4's guard on role change.
        // Without this, an Admin could deactivate every other Admin one by one.
        if (!cmd.IsActive && snapshot.IsActive && snapshot.Role == UserRole.Admin)
        {
            var otherActiveAdmins = await _uow.Repository<User>()
                .LongCountAsync(u => u.Role == UserRole.Admin && u.IsActive && u.Id != snapshot.Id, ct);

            if (otherActiveAdmins == 0)
            {
                throw new BadRequestException(
                    "Cannot deactivate the last Admin. Promote another user first.");
            }
        }

        // Idempotent path — target state already matches. On deactivate, STILL revoke tokens
        // (an Admin re-running deactivate is a legitimate "session kick" for a suspected compromise).
        if (snapshot.IsActive == cmd.IsActive)
        {
            if (!cmd.IsActive)
            {
                await _authPersistence.RevokeAllUserTokensAsync(snapshot.Id, _clock.UtcNow, ct);
                _logger.LogInformation(
                    "Admin {AdminId} re-issued deactivate for already-inactive user {TargetId} (session kick).",
                    _currentUser.UserId, snapshot.Id);
            }
            return;
        }

        // Phase 2: state change → now load tracked so EF sees the mutation.
        var user = await _uow.Repository<User>().FirstOrDefaultAsync(u => u.Id == cmd.UserId, ct);
        if (user is null)
        {
            // Racy delete between phase 1 and phase 2 — surface as NotFound.
            throw new NotFoundException("User not found.");
        }

        if (cmd.IsActive)
        {
            user.Reactivate();
        }
        else
        {
            user.Deactivate();
        }

        await _uow.SaveChangesAsync(ct);

        if (!cmd.IsActive)
        {
            await _authPersistence.RevokeAllUserTokensAsync(user.Id, _clock.UtcNow, ct);
        }

        _logger.LogInformation(
            "Admin {AdminId} {Event} user {TargetId}.",
            _currentUser.UserId,
            cmd.IsActive ? "UserReactivated" : "UserDeactivated",
            user.Id);
    }
}
