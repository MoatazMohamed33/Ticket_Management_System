using FluentAssertions;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.UnitTests.Domain.Tickets;

public class ActivityTimelineEntryTests
{
    private static readonly DateTimeOffset _now = new(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_returns_entry_with_matching_fields()
    {
        var ticketId = Guid.NewGuid();
        var actor = Guid.NewGuid();

        var e = ActivityTimelineEntry.Create(
            ticketId, actor, ActivityEventType.StatusChanged, "Status: Open → InProgress", _now);

        e.Id.Should().NotBe(Guid.Empty);
        e.TicketId.Should().Be(ticketId);
        e.ActorUserId.Should().Be(actor);
        e.Event.Should().Be(ActivityEventType.StatusChanged);
        e.Summary.Should().Be("Status: Open → InProgress");
        e.At.Should().Be(_now);
    }

    [Fact]
    public void Create_empty_ticketId_throws()
    {
        Action act = () => ActivityTimelineEntry.Create(
            Guid.Empty, Guid.NewGuid(), ActivityEventType.TicketCreated, "s", _now);
        act.Should().Throw<ArgumentException>().WithMessage("*TicketId*");
    }

    [Fact]
    public void Create_null_summary_stored_as_empty_string()
    {
        var e = ActivityTimelineEntry.Create(
            Guid.NewGuid(), Guid.NewGuid(), ActivityEventType.TicketCreated, null!, _now);
        e.Summary.Should().Be("");
    }

    [Fact]
    public void Create_allows_empty_actor_id()
    {
        // System-emitted activity (future) may have Guid.Empty actor. Factory doesn't guard.
        var e = ActivityTimelineEntry.Create(
            Guid.NewGuid(), Guid.Empty, ActivityEventType.TicketCreated, "system", _now);
        e.ActorUserId.Should().Be(Guid.Empty);
    }
}
