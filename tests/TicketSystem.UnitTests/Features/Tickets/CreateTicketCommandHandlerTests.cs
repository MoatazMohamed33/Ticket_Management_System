using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TicketSystem.Application.Abstractions.Caching;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Realtime;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Features.Tickets.Commands.CreateTicket;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.UnitTests.Features.Tickets;

public class CreateTicketCommandHandlerTests
{
    private static readonly Guid _customerId = Guid.NewGuid();
    private static readonly DateTimeOffset _now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IGenericRepository<Ticket> _ticketRepo = Substitute.For<IGenericRepository<Ticket>>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IDashboardCacheInvalidator _cache = Substitute.For<IDashboardCacheInvalidator>();
    private readonly ITicketBroadcaster _broadcaster = Substitute.For<ITicketBroadcaster>();

    private CreateTicketCommandHandler CreateSut()
    {
        _uow.Repository<Ticket>().Returns(_ticketRepo);
        _clock.UtcNow.Returns(_now);
        _currentUser.UserId.Returns(_customerId);
        return new CreateTicketCommandHandler(
            _uow, _currentUser, _clock, _cache, _broadcaster,
            NullLogger<CreateTicketCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_creates_open_ticket_owned_by_current_user()
    {
        var sut = CreateSut();
        var cmd = new CreateTicketCommand("Cannot login", "Password reset link expired.", TicketPriority.High);

        var dto = await sut.Handle(cmd, default);

        dto.Title.Should().Be("Cannot login");
        dto.Description.Should().Be("Password reset link expired.");
        dto.Priority.Should().Be(TicketPriority.High);
        dto.Status.Should().Be(TicketStatus.Open);
        dto.CustomerId.Should().Be(_customerId);
        dto.AssignedAgentId.Should().BeNull();
        dto.CreatedAt.Should().Be(_now);
        dto.UpdatedAt.Should().Be(_now);
    }

    [Fact]
    public async Task Handle_persists_and_saves_via_unit_of_work()
    {
        var sut = CreateSut();
        var cmd = new CreateTicketCommand("Title", "Desc", TicketPriority.Low);

        await sut.Handle(cmd, default);

        await _ticketRepo.Received(1).AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_invalidates_dashboard_cache_after_save()
    {
        var sut = CreateSut();
        await sut.Handle(new CreateTicketCommand("t", "d", TicketPriority.Medium), default);

        _cache.Received(1).Invalidate();
    }

    [Fact]
    public async Task Handle_broadcasts_TicketCreated_with_customer_id()
    {
        var sut = CreateSut();
        Ticket? added = null;
        await _ticketRepo.AddAsync(Arg.Do<Ticket>(t => added = t), Arg.Any<CancellationToken>());

        await sut.Handle(new CreateTicketCommand("t", "d", TicketPriority.Medium), default);

        await _broadcaster.Received(1).TicketCreatedAsync(
            added!.Id, _customerId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_throws_when_current_user_is_null()
    {
        _uow.Repository<Ticket>().Returns(_ticketRepo);
        _clock.UtcNow.Returns(_now);
        _currentUser.UserId.Returns((Guid?)null);
        var sut = new CreateTicketCommandHandler(
            _uow, _currentUser, _clock, _cache, _broadcaster,
            NullLogger<CreateTicketCommandHandler>.Instance);

        await FluentActions.Invoking(() =>
                sut.Handle(new CreateTicketCommand("t", "d", TicketPriority.Low), default))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        await _ticketRepo.DidNotReceive().AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
