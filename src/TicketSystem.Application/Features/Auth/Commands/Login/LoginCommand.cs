using MediatR;

namespace TicketSystem.Application.Features.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResponseDto>;
