using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TicketSystem.Application.Abstractions.Caching;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Realtime;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Application.Features.Tickets;
using TicketSystem.Application.Features.Tickets.Commands.AssignTicket;
using TicketSystem.Domain.Tickets;
using TicketSystem.Domain.Users;

namespace TicketSystem.UnitTests.Features.Tickets;

public class AssignTicketCommandHandlerTests
{
    private static readonly Guid _ticketId = Guid.NewGuid();
    private static readonly Guid _customerId = Guid.NewGuid();
    private static readonly Guid _adminId = Guid.NewGuid();
    private static readonly Guid _agentId = Guid.NewGuid();
    private static readonly DateTimeOffset _now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
    private const string _rowVersion = "AAAAAAAAB9E=";

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IGenericRepository<Ticket> _ticketRepo = Substitute.For<IGenericRepository<Ticket>>();
    private readonly IGenericRepository<User> _userRepo = Substitute.For<IGenericRepository<User>>();
    private readonly IGenericRepository<ActivityTimelineEntry> _activityRepo = Substitute.For<IGenericRepository<ActivityTimelineEntry>>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IUserSummaryQueries _summaries = Substitute.For<IUserSummaryQueries>();
    private readonly IDashboardCacheInvalidator _cache = Substitute.For<IDashboardCacheInvalidator>();
    private readonly ITicketBroadcaster _broadcaster = Substitute.For<ITicketBroadcaster>();

    private AssignTicketCommandHandler CreateSut(Ticket? existing, User? agent = null)
    {
        _uow.Repository<Ticket>().Returns(_ticketRepo);
        _uow.Repository<User>().Returns(_userRepo);
        _uow.Repository<ActivityTimelineEntry>().Returns(_activityRepo);
        _clock.UtcNow.Returns(_now);
        _currentUser.UserId.Returns(_adminId);
        _currentUser.Role.Returns("Admin");
        _ticketRepo
            .FirstOrDefaultAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Ticket, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(existing);
        _userRepo
            .FirstOrDefaultNoTrackingAsync(Arg.Any<System.Linq.Expressions.Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(agent);
        _summaries.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new UserSummaryDto(ci.ArgAt<Guid>(0), "Agent Smith"));
        return new AssignTicketCommandHandler(
            _uow, _currentUser, _clock, _summaries, _cache, _broadcaster,
            NullLogger<AssignTicketCommandHandler>.Instance);
    }

    private static Ticket UnassignedTicket()
    {
        return Ticket.Create("t", "d", TicketPriority.Medium, _customerId,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    private static User ActiveAgent(Guid id) =>
        User.CreateStaff("agent@example.com", "Agent Smith", UserRole.SupportAgent, "hash");

    [Fact]
    public async Task Handle_first_assign_records_AgentAssigned_activity_with_Assigned_summary()
    {
        var ticket = UnassignedTicket();
        var sut = CreateSut(ticket, ActiveAgent(_agentId));
        var cmd = new AssignTicketCommand(_ticketId, _agentId, _rowVersion);

        await sut.Handle(cmd, default);

        await _activityRepo.Received(1).AddAsync(
            Arg.Is<ActivityTimelineEntry>(a =>
                a.Event == ActivityEventType.AgentAssigned &&
                a.Summary.StartsWith("Assigned to")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_unassign_records_AgentUnassigned_activity()
    {
        var ticket = UnassignedTicket();
        ticket.Assign(_agentId, DateTimeOffset.UtcNow);
        var sut = CreateSut(ticket);   // no lookup needed when unassigning
        var cmd = new AssignTicketCommand(_ticketId, null, _rowVersion);

        await sut.Handle(cmd, default);

        await _activityRepo.Received(1).AddAsync(
            Arg.Is<ActivityTimelineEntry>(a =>
                a.Event == ActivityEventType.AgentUnassigned &&
                a.Summary.StartsWith("Unassigned")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_reassign_records_AgentAssigned_with_Reassigned_summary()
    {
        var oldAgent = Guid.NewGuid();
        var ticket = UnassignedTicket();
        ticket.Assign(oldAgent, DateTimeOffset.UtcNow);
        var sut = CreateSut(ticket, ActiveAgent(_agentId));
        var cmd = new AssignTicketCommand(_ticketId, _agentId, _rowVersion);

        await sut.Handle(cmd, default);

        await _activityRepo.Received(1).AddAsync(
            Arg.Is<ActivityTimelineEntry>(a =>
                a.Event == ActivityEventType.AgentAssigned &&
                a.Summary.StartsWith("Reassigned from")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_same_assignee_is_idempotent_no_activity()
    {
        var ticket = UnassignedTicket();
        ticket.Assign(_agentId, DateTimeOffset.UtcNow);
        var sut = CreateSut(ticket, ActiveAgent(_agentId));
        var cmd = new AssignTicketCommand(_ticketId, _agentId, _rowVersion);

        await sut.Handle(cmd, default);

        await _activityRepo.DidNotReceive().AddAsync(Arg.Any<ActivityTimelineEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_rejects_assignee_who_is_not_support_agent()
    {
        var ticket = UnassignedTicket();
        var customerUser = User.CreateCustomer("c@x.com", "Cust", "hash");
        var sut = CreateSut(ticket, customerUser);
        var cmd = new AssignTicketCommand(_ticketId, _agentId, _rowVersion);

        await FluentActions.Invoking(() => sut.Handle(cmd, default))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_rejects_inactive_agent()
    {
        var ticket = UnassignedTicket();
        var agent = ActiveAgent(_agentId);
        agent.Deactivate();
        var sut = CreateSut(ticket, agent);
        var cmd = new AssignTicketCommand(_ticketId, _agentId, _rowVersion);

        await FluentActions.Invoking(() => sut.Handle(cmd, default))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_rejects_unknown_assignee()
    {
        var ticket = UnassignedTicket();
        var sut = CreateSut(ticket, agent: null);   // user not found
        var cmd = new AssignTicketCommand(_ticketId, _agentId, _rowVersion);

        await FluentActions.Invoking(() => sut.Handle(cmd, default))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_broadcasts_assignment_with_old_and_new_ids()
    {
        var oldAgent = Guid.NewGuid();
        var ticket = UnassignedTicket();
        ticket.Assign(oldAgent, DateTimeOffset.UtcNow);
        var sut = CreateSut(ticket, ActiveAgent(_agentId));
        var cmd = new AssignTicketCommand(_ticketId, _agentId, _rowVersion);

        await sut.Handle(cmd, default);

        await _broadcaster.Received(1).TicketAssignedAsync(
            ticket.Id, oldAgent, _agentId, _customerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_404_when_ticket_missing()
    {
        var sut = CreateSut(existing: null);
        var cmd = new AssignTicketCommand(_ticketId, _agentId, _rowVersion);

        await FluentActions.Invoking(() => sut.Handle(cmd, default))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_invalidates_dashboard_cache()
    {
        var ticket = UnassignedTicket();
        var sut = CreateSut(ticket, ActiveAgent(_agentId));
        await sut.Handle(new AssignTicketCommand(_ticketId, _agentId, _rowVersion), default);

        _cache.Received(1).Invalidate();
    }
}
