using FluentAssertions;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Features.Tickets;

namespace TicketSystem.UnitTests.Features.Tickets;

public class TicketAccessTests
{
    private static readonly Guid _customerId = Guid.NewGuid();
    private static readonly Guid _agentId = Guid.NewGuid();
    private static readonly Guid _otherAgentId = Guid.NewGuid();
    private static readonly Guid _otherCustomerId = Guid.NewGuid();
    private static readonly Guid _adminId = Guid.NewGuid();

    // ---- CanView (Story 4.3) ---------------------------------------------

    [Fact]
    public void CanView_Admin_can_view_any_ticket()
    {
        var caller = MakeUser(_adminId, "Admin");
        TicketAccess.CanView(_customerId, _agentId, caller).Should().BeTrue();
        TicketAccess.CanView(_customerId, null,      caller).Should().BeTrue();
    }

    [Fact]
    public void CanView_Customer_can_view_own_ticket()
    {
        var caller = MakeUser(_customerId, "Customer");
        TicketAccess.CanView(_customerId, null, caller).Should().BeTrue();
    }

    [Fact]
    public void CanView_Customer_cannot_view_other_customers_ticket()
    {
        var caller = MakeUser(_customerId, "Customer");
        TicketAccess.CanView(_otherCustomerId, null, caller).Should().BeFalse();
    }

    [Fact]
    public void CanView_Agent_can_view_assigned_ticket()
    {
        var caller = MakeUser(_agentId, "SupportAgent");
        TicketAccess.CanView(_customerId, _agentId, caller).Should().BeTrue();
    }

    [Fact]
    public void CanView_Agent_can_view_unassigned_ticket()
    {
        var caller = MakeUser(_agentId, "SupportAgent");
        TicketAccess.CanView(_customerId, null, caller).Should().BeTrue();
    }

    [Fact]
    public void CanView_Agent_cannot_view_ticket_assigned_to_another_agent()
    {
        var caller = MakeUser(_agentId, "SupportAgent");
        TicketAccess.CanView(_customerId, _otherAgentId, caller).Should().BeFalse();
    }

    [Fact]
    public void CanView_null_UserId_returns_false()
    {
        var caller = MakeUser(null, "Admin");
        TicketAccess.CanView(_customerId, _agentId, caller).Should().BeFalse();
    }

    [Fact]
    public void CanView_unknown_role_returns_false()
    {
        var caller = MakeUser(_customerId, "SuperUser");
        TicketAccess.CanView(_customerId, null, caller).Should().BeFalse();
    }

    [Fact]
    public void CanView_null_role_returns_false()
    {
        var caller = MakeUser(_customerId, null);
        TicketAccess.CanView(_customerId, null, caller).Should().BeFalse();
    }

    // ---- CanMutateStatus (Story 5.2) — stricter than CanView ------------

    [Fact]
    public void CanMutateStatus_Admin_can_always_mutate()
    {
        var caller = MakeUser(_adminId, "Admin");
        TicketAccess.CanMutateStatus(_customerId, _agentId, caller).Should().BeTrue();
        TicketAccess.CanMutateStatus(_customerId, null, caller).Should().BeTrue();
    }

    [Fact]
    public void CanMutateStatus_Agent_assigned_can_mutate()
    {
        var caller = MakeUser(_agentId, "SupportAgent");
        TicketAccess.CanMutateStatus(_customerId, _agentId, caller).Should().BeTrue();
    }

    [Fact]
    public void CanMutateStatus_Agent_unassigned_cannot_mutate()
    {
        // Key difference from CanView — Agent viewing an unassigned ticket for triage
        // must not be able to change its state without being assigned.
        var caller = MakeUser(_agentId, "SupportAgent");
        TicketAccess.CanMutateStatus(_customerId, null, caller).Should().BeFalse();
    }

    [Fact]
    public void CanMutateStatus_Agent_assigned_elsewhere_cannot_mutate()
    {
        var caller = MakeUser(_agentId, "SupportAgent");
        TicketAccess.CanMutateStatus(_customerId, _otherAgentId, caller).Should().BeFalse();
    }

    [Fact]
    public void CanMutateStatus_Customer_never_can_mutate()
    {
        var caller = MakeUser(_customerId, "Customer");
        TicketAccess.CanMutateStatus(_customerId, null, caller).Should().BeFalse();
    }

    [Fact]
    public void CanMutateStatus_null_UserId_returns_false()
    {
        var caller = MakeUser(null, "Admin");
        TicketAccess.CanMutateStatus(_customerId, _agentId, caller).Should().BeFalse();
    }

    private static ICurrentUser MakeUser(Guid? userId, string? role) =>
        new StubCurrentUser { UserId = userId, Role = role };

    private sealed class StubCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
        public string? Role { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
    }
}
