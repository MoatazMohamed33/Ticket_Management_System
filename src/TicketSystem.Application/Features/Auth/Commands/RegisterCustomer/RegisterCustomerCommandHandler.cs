using MediatR;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Application.Features.Auth.Commands.Login;
using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.Auth.Commands.RegisterCustomer;

public sealed class RegisterCustomerCommandHandler : IRequestHandler<RegisterCustomerCommand, UserDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;

    public RegisterCustomerCommandHandler(IUnitOfWork uow, IPasswordHasher hasher)
    {
        _uow = uow;
        _hasher = hasher;
    }

    public async Task<UserDto> Handle(RegisterCustomerCommand cmd, CancellationToken ct)
    {
        var normalized = cmd.Email.Trim().ToUpperInvariant();

        var exists = await _uow.Repository<User>()
            .AnyAsync(u => u.EmailNormalized == normalized, ct);
        if (exists)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = User.CreateCustomer(cmd.Email.Trim(), cmd.DisplayName.Trim(), _hasher.Hash(cmd.Password));

        await _uow.Repository<User>().AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return new UserDto(user.Id, user.Email, user.DisplayName, user.Role);
    }
}
