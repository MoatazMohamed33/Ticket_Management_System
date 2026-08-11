using MediatR;
using TicketSystem.Application.Features.Auth.Commands.Login;

namespace TicketSystem.Application.Features.Auth.Commands.RegisterCustomer;

public sealed record RegisterCustomerCommand(string Email, string DisplayName, string Password) : IRequest<UserDto>;
