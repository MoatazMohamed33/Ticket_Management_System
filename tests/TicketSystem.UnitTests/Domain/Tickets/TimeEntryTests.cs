using FluentAssertions;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.UnitTests.Domain.Tickets;

public class TimeEntryTests
{
    private static readonly DateTimeOffset _now = new(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly _today = DateOnly.FromDateTime(_now.UtcDateTime);

    [Fact]
    public void Create_returns_entry_with_matching_fields()
    {
        var ticketId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var e = TimeEntry.Create(ticketId, authorId, _today, 30, "worked", _now);

        e.Id.Should().NotBe(Guid.Empty);
        e.TicketId.Should().Be(ticketId);
        e.AuthorUserId.Should().Be(authorId);
        e.WorkedOn.Should().Be(_today);
        e.DurationMinutes.Should().Be(30);
        e.Description.Should().Be("worked");
        e.CreatedAt.Should().Be(_now);
    }

    [Fact]
    public void Create_trims_description()
    {
        var e = TimeEntry.Create(Guid.NewGuid(), Guid.NewGuid(), _today, 30, "  work  ", _now);
        e.Description.Should().Be("work");
    }

    [Fact]
    public void Create_empty_ticketId_throws()
    {
        Action act = () => TimeEntry.Create(Guid.Empty, Guid.NewGuid(), _today, 30, "w", _now);
        act.Should().Throw<ArgumentException>().WithMessage("*TicketId*");
    }

    [Fact]
    public void Create_empty_authorId_throws()
    {
        Action act = () => TimeEntry.Create(Guid.NewGuid(), Guid.Empty, _today, 30, "w", _now);
        act.Should().Throw<ArgumentException>().WithMessage("*AuthorUserId*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_non_positive_duration_throws(int minutes)
    {
        Action act = () => TimeEntry.Create(Guid.NewGuid(), Guid.NewGuid(), _today, minutes, "w", _now);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("durationMinutes");
    }

    [Theory]
    [InlineData(1441)]     // 24h + 1min
    [InlineData(2880)]     // 48h
    [InlineData(int.MaxValue)]
    public void Create_over_max_duration_throws(int minutes)
    {
        Action act = () => TimeEntry.Create(Guid.NewGuid(), Guid.NewGuid(), _today, minutes, "w", _now);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("durationMinutes");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(1440)]     // exactly 24h — boundary allowed
    public void Create_boundary_durations_allowed(int minutes)
    {
        var e = TimeEntry.Create(Guid.NewGuid(), Guid.NewGuid(), _today, minutes, "w", _now);
        e.DurationMinutes.Should().Be(minutes);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_blank_description_throws(string? desc)
    {
        Action act = () => TimeEntry.Create(Guid.NewGuid(), Guid.NewGuid(), _today, 30, desc!, _now);
        act.Should().Throw<ArgumentException>().WithMessage("*Description*");
    }
}
