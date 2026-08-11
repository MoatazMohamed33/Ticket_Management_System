using MediatR;
using Microsoft.Extensions.Logging;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.AdminUsers.Commands.ChangeUserRole;

public sealed class ChangeUserRoleCommandHandler : IRequestHandler<ChangeUserRoleCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IAuthPersistence _authPersistence;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly ILogger<ChangeUserRoleCommandHandler> _logger;

    public ChangeUserRoleCommandHandler(
        IUnitOfWork uow,
        IAuthPersistence authPersistence,
        ICurrentUser currentUser,
        IClock clock,
        ILogger<ChangeUserRoleCommandHandler> logger)
    {
        _uow = uow;
        _authPersistence = authPersistence;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task Handle(ChangeUserRoleCommand cmd, CancellationToken ct)
    {
        var user = await _uow.Repository<User>().FirstOrDefaultAsync(u => u.Id == cmd.UserId, ct);
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        var oldRole = user.Role;

        // Idempotent no-op — do not revoke tokens or log a state change.
        if (oldRole == cmd.Role)
        {
            return;
        }

        // Self role change guard — only self-to-different-role is blocked. Self-to-same is
        // caught by the idempotent no-op above.
        if (_currentUser.UserId is Guid callerId && callerId == cmd.UserId)
        {
            throw new BadRequestException("You cannot change your own role.");
        }

        // Last-Admin guard — cannot demote the last active Admin.
        if (oldRole == UserRole.Admin && cmd.Role != UserRole.Admin)
        {
            var otherActiveAdmins = await _uow.Repository<User>()
                .LongCountAsync(u => u.Role == UserRole.Admin && u.IsActive && u.Id != user.Id, ct);

            if (otherActiveAdmins == 0)
            {
                throw new BadRequestException(
                    "Cannot remove the last Admin. Promote another user first.");
            }
        }

        user.ChangeRole(cmd.Role);
        await _uow.SaveChangesAsync(ct);

        // Revoke tokens so the user re-logs in and gets a JWT with the new role claim.
        await _authPersistence.RevokeAllUserTokensAsync(user.Id, _clock.UtcNow, ct);

        _logger.LogInformation(
            "Admin {AdminId} changed role of user {TargetId} from {OldRole} to {NewRole}.",
            _currentUser.UserId, user.Id, oldRole, cmd.Role);
    }
}
