namespace TicketSystem.Application.Features.Tickets;

/// <summary>Minimal user projection for embedded author/actor/customer fields. Never
/// includes email, role, or any credential-adjacent field.</summary>
public sealed record UserSummaryDto(Guid Id, string DisplayName);
