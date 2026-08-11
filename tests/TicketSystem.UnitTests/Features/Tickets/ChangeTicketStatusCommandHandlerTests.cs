using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TicketSystem.Application.Abstractions.Caching;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Realtime;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Application.Features.Tickets.Commands.ChangeTicketStatus;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.UnitTests.Features.Tickets;

public class ChangeTicketStatusCommandHandlerTests
{
    private static readonly Guid _ticketId = Guid.NewGuid();
    private static readonly Guid _customerId = Guid.NewGuid();
    private static readonly Guid _agentId = Guid.NewGuid();
    private static readonly Guid _adminId = Guid.NewGuid();
    private static readonly DateTimeOffset _now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
    private const string _rowVersion = "AAAAAAAAB9E=";

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IGenericRepository<Ticket> _ticketRepo = Substitute.For<IGenericRepository<Ticket>>();
    private readonly IGenericRepository<ActivityTimelineEntry> _activityRepo = Substitute.For<IGenericRepository<ActivityTimelineEntry>>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IDashboardCacheInvalidator _cache = Substitute.For<IDashboardCacheInvalidator>();
    private readonly ITicketBroadcaster _broadcaster = Substitute.For<ITicketBroadcaster>();

    private ChangeTicketStatusCommandHandler CreateSut(Guid callerId, string role, Ticket? existing)
    {
        _uow.Repository<Ticket>().Returns(_ticketRepo);
        _uow.Repository<ActivityTimelineEntry>().Returns(_activityRepo);
        _clock.UtcNow.Returns(_now);
        _currentUser.UserId.Returns(callerId);
        _currentUser.Role.Returns(role);
        _ticketRepo
            .FirstOrDefaultAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Ticket, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(existing);
        return new ChangeTicketStatusCommandHandler(
            _uow, _currentUser, _clock, _cache, _broadcaster,
            NullLogger<ChangeTicketStatusCommandHandler>.Instance);
    }

    private static Ticket TicketInStatus(TicketStatus target, Guid? assignedAgentId)
    {
        var t = Ticket.Create("Title", "Desc", TicketPriority.Medium, _customerId,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        if (assignedAgentId.HasValue) t.Assign(assignedAgentId, DateTimeOffset.UtcNow);
        if (target == TicketStatus.Open) return t;
        t.ChangeStatus(TicketStatus.InProgress, DateTimeOffset.UtcNow);
        if (target == TicketStatus.InProgress) return t;
        t.ChangeStatus(TicketStatus.Resolved, DateTimeOffset.UtcNow);
        if (target == TicketStatus.Resolved) return t;
        t.ChangeStatus(TicketStatus.Closed, DateTimeOffset.UtcNow);
        return t;
    }

    [Fact]
    public async Task Handle_admin_transitions_status_and_records_activity()
    {
        var ticket = TicketInStatus(TicketStatus.Open, _agentId);
        var sut = CreateSut(_adminId, "Admin", ticket);
        var cmd = new ChangeTicketStatusCommand(_ticketId, TicketStatus.InProgress, _rowVersion);

        var dto = await sut.Handle(cmd, default);

        dto.Status.Should().Be(TicketStatus.InProgress);
        await _activityRepo.Received(1).AddAsync(
            Arg.Is<ActivityTimelineEntry>(a =>
                a.Event == ActivityEventType.StatusChanged &&
                a.ActorUserId == _adminId &&
                a.Summary.Contains("Open") &&
                a.Summary.Contains("InProgress")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_agent_assigned_can_transition()
    {
        var ticket = TicketInStatus(TicketStatus.Open, _agentId);
        var sut = CreateSut(_agentId, "SupportAgent", ticket);
        var cmd = new ChangeTicketStatusCommand(_ticketId, TicketStatus.InProgress, _rowVersion);

        var dto = await sut.Handle(cmd, default);

        dto.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public async Task Handle_agent_unassigned_returns_404_via_no_leak()
    {
        // Agent viewing an unassigned ticket can VIEW but not MUTATE — 404 (not 403) preserves no-leak.
        var ticket = TicketInStatus(TicketStatus.Open, null);
        var sut = CreateSut(_agentId, "SupportAgent", ticket);
        var cmd = new ChangeTicketStatusCommand(_ticketId, TicketStatus.InProgress, _rowVersion);

        await FluentActions.Invoking(() => sut.Handle(cmd, default))
            .Should().ThrowAsync<NotFoundException>();

        await _activityRepo.DidNotReceive().AddAsync(Arg.Any<ActivityTimelineEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_agent_assigned_elsewhere_returns_404()
    {
        var otherAgent = Guid.NewGuid();
        var ticket = TicketInStatus(TicketStatus.Open, otherAgent);
        var sut = CreateSut(_agentId, "SupportAgent", ticket);
        var cmd = new ChangeTicketStatusCommand(_ticketId, TicketStatus.InProgress, _rowVersion);

        await FluentActions.Invoking(() => sut.Handle(cmd, default))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_returns_404_when_ticket_missing()
    {
        var sut = CreateSut(_adminId, "Admin", existing: null);
        var cmd = new ChangeTicketStatusCommand(_ticketId, TicketStatus.InProgress, _rowVersion);

        await FluentActions.Invoking(() => sut.Handle(cmd, default))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_illegal_transition_bubbles_domain_exception()
    {
        var ticket = TicketInStatus(TicketStatus.Open, _agentId);
        var sut = CreateSut(_adminId, "Admin", ticket);
        // Open → Resolved is illegal per LegalTransitions.
        var cmd = new ChangeTicketStatusCommand(_ticketId, TicketStatus.Resolved, _rowVersion);

        await FluentActions.Invoking(() => sut.Handle(cmd, default))
            .Should().ThrowAsync<InvalidTicketTransitionException>();
    }

    [Fact]
    public async Task Handle_same_status_is_idempotent_no_activity_no_broadcast_state()
    {
        var ticket = TicketInStatus(TicketStatus.InProgress, _agentId);
        var sut = CreateSut(_adminId, "Admin", ticket);
        var cmd = new ChangeTicketStatusCommand(_ticketId, TicketStatus.InProgress, _rowVersion);

        var dto = await sut.Handle(cmd, default);

        dto.Status.Should().Be(TicketStatus.InProgress);
        await _activityRepo.DidNotReceive().AddAsync(Arg.Any<ActivityTimelineEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_close_transition_uses_Closed_change_kind_in_broadcast()
    {
        var ticket = TicketInStatus(TicketStatus.Resolved, _agentId);
        var sut = CreateSut(_adminId, "Admin", ticket);
        var cmd = new ChangeTicketStatusCommand(_ticketId, TicketStatus.Closed, _rowVersion);

        await sut.Handle(cmd, default);

        await _broadcaster.Received(1).TicketUpdatedAsync(
            ticket.Id, "Closed", _customerId, _agentId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_non_close_transition_uses_Status_change_kind_in_broadcast()
    {
        var ticket = TicketInStatus(TicketStatus.InProgress, _agentId);
        var sut = CreateSut(_adminId, "Admin", ticket);
        var cmd = new ChangeTicketStatusCommand(_ticketId, TicketStatus.Resolved, _rowVersion);

        await sut.Handle(cmd, default);

        await _broadcaster.Received(1).TicketUpdatedAsync(
            ticket.Id, "Status", _customerId, _agentId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_sets_original_concurrency_token_from_rowversion()
    {
        var ticket = TicketInStatus(TicketStatus.Open, _agentId);
        var sut = CreateSut(_adminId, "Admin", ticket);
        var cmd = new ChangeTicketStatusCommand(_ticketId, TicketStatus.InProgress, _rowVersion);

        await sut.Handle(cmd, default);

        var expected = Convert.FromBase64String(_rowVersion);
        _uow.Received(1).SetOriginalConcurrencyToken(
            ticket, nameof(Ticket.RowVersion),
            Arg.Is<byte[]>(b => b.SequenceEqual(expected)));
    }

    [Fact]
    public async Task Handle_invalidates_dashboard_cache_after_save()
    {
        var ticket = TicketInStatus(TicketStatus.Open, _agentId);
        var sut = CreateSut(_adminId, "Admin", ticket);
        await sut.Handle(new ChangeTicketStatusCommand(_ticketId, TicketStatus.InProgress, _rowVersion), default);

        _cache.Received(1).Invalidate();
    }
}
