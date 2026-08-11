using TicketSystem.Application.Abstractions.Security;

namespace TicketSystem.Application.Features.Tickets;

/// <summary>
/// Single source of truth for "can this caller see this ticket?" — reused by 4.3/4.4/4.5
/// and Epic 5. Compares against string role names (matching the JWT `role` claim shape),
/// NOT the UserRole enum, so ICurrentUser.Role (which is string?) compares directly.
/// </summary>
public static class TicketAccess
{
    public const string Admin = "Admin";
    public const string SupportAgent = "SupportAgent";
    public const string Customer = "Customer";

    public static bool CanView(TicketDetailDto t, ICurrentUser caller) =>
        CanView(t.Customer.Id, t.AssignedAgent?.Id, caller);

    public static bool CanView(Guid customerId, Guid? assignedAgentId, ICurrentUser caller)
    {
        if (caller.UserId is not Guid callerId) return false;
        return caller.Role switch
        {
            Admin        => true,
            Customer     => customerId == callerId,
            // Agent sees assigned OR unassigned tickets (triage queue is legitimate).
            SupportAgent => assignedAgentId == callerId || assignedAgentId is null,
            _            => false,
        };
    }

    /// <summary>
    /// Stricter than the CanView helpers: an Agent must be ASSIGNED to mutate the
    /// ticket (unassigned = triage-view only, no state changes). Admins always allowed.
    /// Customers always forbidden — they use dedicated endpoints (Close in Story 4.5).
    /// Used by Story 5.2 status endpoint, Story 5.5 time-log, Epic 6 priority/assign.
    /// </summary>
    public static bool CanMutateStatus(Guid customerId, Guid? assignedAgentId, ICurrentUser caller)
    {
        if (caller.UserId is not Guid callerId) return false;
        return caller.Role switch
        {
            Admin        => true,
            SupportAgent => assignedAgentId == callerId,
            _            => false,
        };
    }
}
