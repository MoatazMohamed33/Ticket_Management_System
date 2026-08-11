using MediatR;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.AdminUsers.Commands.CreateStaffUser;

public sealed class CreateStaffUserCommandHandler : IRequestHandler<CreateStaffUserCommand, AdminUserDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;

    public CreateStaffUserCommandHandler(IUnitOfWork uow, IPasswordHasher hasher)
    {
        _uow = uow;
        _hasher = hasher;
    }

    public async Task<AdminUserDto> Handle(CreateStaffUserCommand cmd, CancellationToken ct)
    {
        var normalized = cmd.Email.Trim().ToUpperInvariant();

        var exists = await _uow.Repository<User>()
            .AnyAsync(u => u.EmailNormalized == normalized, ct);
        if (exists)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = User.CreateStaff(
            cmd.Email.Trim(),
            cmd.DisplayName.Trim(),
            cmd.Role,
            _hasher.Hash(cmd.InitialPassword));

        await _uow.Repository<User>().AddAsync(user, ct);
        // NOTE: the definitive uniqueness guard is the DB unique index on EmailNormalized.
        // Concurrent POSTs that both pass the AnyAsync check will race — the second
        // SaveChangesAsync throws DbUpdateException, mapped to 409 by
        // ExceptionHandlingMiddleware (see Api layer). AC-4 (409 on duplicate) is preserved.
        await _uow.SaveChangesAsync(ct);

        return user.ToAdminUserDto();
    }
}
