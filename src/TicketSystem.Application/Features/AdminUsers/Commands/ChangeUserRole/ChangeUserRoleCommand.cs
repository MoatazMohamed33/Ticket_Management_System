using MediatR;
using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.AdminUsers.Commands.ChangeUserRole;

public sealed record ChangeUserRoleCommand(Guid UserId, UserRole Role) : IRequest;
