using MediatR;
using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.AdminUsers.Commands.CreateStaffUser;

public sealed record CreateStaffUserCommand(
    string Email,
    string DisplayName,
    UserRole Role,
    string InitialPassword) : IRequest<AdminUserDto>;
