using MediatR;
using TicketSystem.Application.Features.Auth.Commands.Login;

namespace TicketSystem.Application.Features.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponseDto>;
