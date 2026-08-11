using MediatR;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Commands.CreateTicket;

/// <summary>
/// Customer creates a ticket. No CustomerId field — caller identity comes from
/// <see cref="Abstractions.Security.ICurrentUser"/> so a body-supplied id can never
/// override the authenticated user (FR24 defense-in-depth).
/// </summary>
public sealed record CreateTicketCommand(
    string Title,
    string Description,
    TicketPriority Priority) : IRequest<TicketDto>;
