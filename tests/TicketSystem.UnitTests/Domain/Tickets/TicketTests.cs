using FluentAssertions;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.UnitTests.Domain.Tickets;

public class TicketTests
{
    private static readonly DateTimeOffset _now = new(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid _customer = Guid.NewGuid();

    // ---- Create factory (Story 4.1) --------------------------------------

    [Fact]
    public void Create_returns_Open_ticket_with_matching_fields()
    {
        var t = Ticket.Create("title", "desc", TicketPriority.High, _customer, _now);

        t.Id.Should().NotBe(Guid.Empty);
        t.Title.Should().Be("title");
        t.Description.Should().Be("desc");
        t.Priority.Should().Be(TicketPriority.High);
        t.Status.Should().Be(TicketStatus.Open);
        t.CustomerId.Should().Be(_customer);
        t.AssignedAgentId.Should().BeNull();
        t.CreatedAt.Should().Be(_now);
        t.UpdatedAt.Should().Be(_now);
        t.ClosedAt.Should().BeNull();
    }

    [Fact]
    public void Create_trims_title_and_description()
    {
        var t = Ticket.Create("  spaced  ", "  desc  ", TicketPriority.Low, _customer, _now);
        t.Title.Should().Be("spaced");
        t.Description.Should().Be("desc");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_blank_title_throws(string? title)
    {
        Action act = () => Ticket.Create(title!, "desc", TicketPriority.Low, _customer, _now);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_blank_description_throws(string? desc)
    {
        Action act = () => Ticket.Create("title", desc!, TicketPriority.Low, _customer, _now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_empty_customerId_throws()
    {
        Action act = () => Ticket.Create("title", "desc", TicketPriority.Low, Guid.Empty, _now);
        act.Should().Throw<ArgumentException>();
    }

    // ---- TouchUpdatedAt (Story 4.4) --------------------------------------

    [Fact]
    public void TouchUpdatedAt_bumps_UpdatedAt_only()
    {
        var t = Ticket.Create("t", "d", TicketPriority.Low, _customer, _now);
        var later = _now.AddMinutes(5);

        t.TouchUpdatedAt(later);

        t.UpdatedAt.Should().Be(later);
        t.CreatedAt.Should().Be(_now);   // unchanged
    }

    // ---- Close (Story 4.5) -----------------------------------------------

    [Fact]
    public void Close_from_Resolved_transitions_to_Closed_bumps_UpdatedAt_and_sets_ClosedAt()
    {
        var t = ResolvedTicket();
        var closeTime = _now.AddHours(1);

        t.Close(closeTime);

        t.Status.Should().Be(TicketStatus.Closed);
        t.UpdatedAt.Should().Be(closeTime);
        t.ClosedAt.Should().Be(closeTime);   // Story 6.4
    }

    [Fact]
    public void Close_when_already_Closed_is_idempotent_no_change()
    {
        var t = ResolvedTicket();
        var firstClose = _now.AddHours(1);
        t.Close(firstClose);

        t.Close(_now.AddHours(5));

        t.Status.Should().Be(TicketStatus.Closed);
        t.UpdatedAt.Should().Be(firstClose);   // NOT bumped again
        t.ClosedAt.Should().Be(firstClose);
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    public void Close_from_non_Resolved_throws_InvalidTicketTransitionException(TicketStatus start)
    {
        var t = TicketWithStatus(start);

        Action act = () => t.Close(_now);
        act.Should().Throw<InvalidTicketTransitionException>()
            .Which.To.Should().Be(TicketStatus.Closed);
    }

    // ---- ChangeStatus state machine (Story 5.2) --------------------------

    public static IEnumerable<object[]> LegalTransitions => new[]
    {
        new object[] { TicketStatus.Open,       TicketStatus.InProgress },
        new object[] { TicketStatus.InProgress, TicketStatus.Resolved   },
        new object[] { TicketStatus.InProgress, TicketStatus.Open       },
        new object[] { TicketStatus.Resolved,   TicketStatus.InProgress },
        new object[] { TicketStatus.Resolved,   TicketStatus.Closed     },
    };

    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public void ChangeStatus_legal_transition_succeeds(TicketStatus from, TicketStatus to)
    {
        var t = TicketWithStatus(from);
        var later = _now.AddMinutes(10);

        t.ChangeStatus(to, later);

        t.Status.Should().Be(to);
        t.UpdatedAt.Should().Be(later);
    }

    public static IEnumerable<object[]> IllegalTransitions
    {
        get
        {
            var all = Enum.GetValues<TicketStatus>();
            foreach (var from in all)
            {
                foreach (var to in all)
                {
                    if (from == to) continue;
                    var isLegal = (from, to) switch
                    {
                        (TicketStatus.Open,       TicketStatus.InProgress) => true,
                        (TicketStatus.InProgress, TicketStatus.Resolved)   => true,
                        (TicketStatus.InProgress, TicketStatus.Open)       => true,
                        (TicketStatus.Resolved,   TicketStatus.InProgress) => true,
                        (TicketStatus.Resolved,   TicketStatus.Closed)     => true,
                        _ => false,
                    };
                    if (!isLegal) yield return new object[] { from, to };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(IllegalTransitions))]
    public void ChangeStatus_illegal_transition_throws(TicketStatus from, TicketStatus to)
    {
        var t = TicketWithStatus(from);

        Action act = () => t.ChangeStatus(to, _now);
        act.Should().Throw<InvalidTicketTransitionException>()
            .Which.Should().Match<InvalidTicketTransitionException>(e =>
                e.From == from && e.To == to);
    }

    [Fact]
    public void ChangeStatus_same_status_is_idempotent_no_UpdatedAt_bump()
    {
        var t = Ticket.Create("t", "d", TicketPriority.Low, _customer, _now);
        var initialUpdatedAt = t.UpdatedAt;

        t.ChangeStatus(TicketStatus.Open, _now.AddHours(1));

        t.Status.Should().Be(TicketStatus.Open);
        t.UpdatedAt.Should().Be(initialUpdatedAt);
    }

    [Fact]
    public void ChangeStatus_to_Closed_sets_ClosedAt()
    {
        var t = ResolvedTicket();
        var closeTime = _now.AddHours(2);

        t.ChangeStatus(TicketStatus.Closed, closeTime);

        t.ClosedAt.Should().Be(closeTime);
    }

    [Fact]
    public void ChangeStatus_to_non_Closed_does_not_touch_ClosedAt()
    {
        var t = Ticket.Create("t", "d", TicketPriority.Low, _customer, _now);

        t.ChangeStatus(TicketStatus.InProgress, _now.AddMinutes(5));

        t.ClosedAt.Should().BeNull();
    }

    // ---- ChangePriority (Story 6.2) --------------------------------------

    [Fact]
    public void ChangePriority_updates_priority_and_bumps_UpdatedAt()
    {
        var t = Ticket.Create("t", "d", TicketPriority.Low, _customer, _now);
        var later = _now.AddMinutes(5);

        t.ChangePriority(TicketPriority.Critical, later);

        t.Priority.Should().Be(TicketPriority.Critical);
        t.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void ChangePriority_same_priority_is_idempotent_no_bump()
    {
        var t = Ticket.Create("t", "d", TicketPriority.High, _customer, _now);
        var initialUpdatedAt = t.UpdatedAt;

        t.ChangePriority(TicketPriority.High, _now.AddMinutes(5));

        t.UpdatedAt.Should().Be(initialUpdatedAt);
    }

    // ---- Assign (Story 6.3) ----------------------------------------------

    [Fact]
    public void Assign_null_to_agentId_bumps_UpdatedAt()
    {
        var t = Ticket.Create("t", "d", TicketPriority.Low, _customer, _now);
        var agent = Guid.NewGuid();
        var later = _now.AddMinutes(5);

        t.Assign(agent, later);

        t.AssignedAgentId.Should().Be(agent);
        t.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void Assign_agentId_to_different_agentId_bumps_UpdatedAt()
    {
        var t = Ticket.Create("t", "d", TicketPriority.Low, _customer, _now);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        t.Assign(first, _now.AddMinutes(1));

        var later = _now.AddMinutes(5);
        t.Assign(second, later);

        t.AssignedAgentId.Should().Be(second);
        t.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void Assign_agentId_to_null_unassigns_and_bumps()
    {
        var t = Ticket.Create("t", "d", TicketPriority.Low, _customer, _now);
        var agent = Guid.NewGuid();
        t.Assign(agent, _now.AddMinutes(1));

        var later = _now.AddMinutes(5);
        t.Assign(null, later);

        t.AssignedAgentId.Should().BeNull();
        t.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void Assign_same_null_is_idempotent()
    {
        var t = Ticket.Create("t", "d", TicketPriority.Low, _customer, _now);
        var initialUpdatedAt = t.UpdatedAt;

        t.Assign(null, _now.AddMinutes(5));

        t.AssignedAgentId.Should().BeNull();
        t.UpdatedAt.Should().Be(initialUpdatedAt);
    }

    [Fact]
    public void Assign_same_agentId_is_idempotent()
    {
        var t = Ticket.Create("t", "d", TicketPriority.Low, _customer, _now);
        var agent = Guid.NewGuid();
        t.Assign(agent, _now.AddMinutes(1));
        var updatedAtAfterAssign = t.UpdatedAt;

        t.Assign(agent, _now.AddMinutes(5));

        t.AssignedAgentId.Should().Be(agent);
        t.UpdatedAt.Should().Be(updatedAtAfterAssign);
    }

    // ---- helpers ---------------------------------------------------------

    private static Ticket ResolvedTicket()
    {
        var t = Ticket.Create("t", "d", TicketPriority.Low, _customer, _now);
        t.ChangeStatus(TicketStatus.InProgress, _now.AddMinutes(1));
        t.ChangeStatus(TicketStatus.Resolved, _now.AddMinutes(2));
        return t;
    }

    private static Ticket TicketWithStatus(TicketStatus target)
    {
        var t = Ticket.Create("t", "d", TicketPriority.Low, _customer, _now);
        switch (target)
        {
            case TicketStatus.Open: return t;
            case TicketStatus.InProgress:
                t.ChangeStatus(TicketStatus.InProgress, _now.AddMinutes(1)); return t;
            case TicketStatus.Resolved:
                t.ChangeStatus(TicketStatus.InProgress, _now.AddMinutes(1));
                t.ChangeStatus(TicketStatus.Resolved, _now.AddMinutes(2)); return t;
            case TicketStatus.Closed:
                t.ChangeStatus(TicketStatus.InProgress, _now.AddMinutes(1));
                t.ChangeStatus(TicketStatus.Resolved, _now.AddMinutes(2));
                t.Close(_now.AddMinutes(3)); return t;
            default: throw new ArgumentOutOfRangeException(nameof(target));
        }
    }
}
