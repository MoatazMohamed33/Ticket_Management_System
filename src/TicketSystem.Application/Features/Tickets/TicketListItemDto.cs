using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets;

/// <summary>
/// Row shape for customer/agent/admin ticket lists. One shape across surfaces —
/// nullable fields let each endpoint populate what makes sense and leave the rest null.
/// <para><see cref="AssignedAgentDisplayName"/> is populated in all list surfaces (Customer
/// wants to know who's helping; Agent/Admin already know).</para>
/// <para><see cref="CustomerDisplayName"/> is populated only in Agent + Admin lists
/// (a Customer looking at their own list already knows who the customer is).</para>
/// </summary>
public sealed record TicketListItemDto(
    Guid Id,
    string Title,
    TicketStatus Status,
    TicketPriority Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? AssignedAgentDisplayName,
    string? CustomerDisplayName,
    Guid? AssignedAgentId,
    string RowVersion);
