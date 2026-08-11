using FluentAssertions;
using NSubstitute;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.Tickets;
using TicketSystem.Application.Features.Tickets.Queries.ListMyTickets;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.UnitTests.Features.Tickets;

public class ListMyTicketsQueryHandlerTests
{
    private static readonly Guid _customerId = Guid.NewGuid();

    private readonly IMyTicketsQueries _queries = Substitute.For<IMyTicketsQueries>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private ListMyTicketsQueryHandler CreateSut(Guid? callerId)
    {
        _currentUser.UserId.Returns(callerId);
        _queries.ListForCustomerAsync(
                Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<TicketStatus[]?>(), Arg.Any<TicketPriority[]?>(),
                Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TicketListItemDto>(Array.Empty<TicketListItemDto>(), 1, 20, 0, 0));
        return new ListMyTicketsQueryHandler(_queries, _currentUser);
    }

    [Fact]
    public async Task Handle_scopes_query_to_current_user()
    {
        var sut = CreateSut(_customerId);
        await sut.Handle(new ListMyTicketsQuery(), default);

        await _queries.Received(1).ListForCustomerAsync(
            _customerId, Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<TicketStatus[]?>(), Arg.Any<TicketPriority[]?>(),
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_throws_when_no_current_user()
    {
        var sut = CreateSut(callerId: null);
        await FluentActions.Invoking(() => sut.Handle(new ListMyTicketsQuery(), default))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_parses_status_csv_into_typed_array()
    {
        var sut = CreateSut(_customerId);
        await sut.Handle(new ListMyTicketsQuery(Status: "Open,InProgress"), default);

        await _queries.Received(1).ListForCustomerAsync(
            _customerId, Arg.Any<int>(), Arg.Any<int>(),
            Arg.Is<TicketStatus[]?>(a => a!.SequenceEqual(new[] { TicketStatus.Open, TicketStatus.InProgress })),
            Arg.Any<TicketPriority[]?>(),
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_parses_priority_csv_into_typed_array()
    {
        var sut = CreateSut(_customerId);
        await sut.Handle(new ListMyTicketsQuery(Priority: "High,Critical"), default);

        await _queries.Received(1).ListForCustomerAsync(
            _customerId, Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<TicketStatus[]?>(),
            Arg.Is<TicketPriority[]?>(a => a!.SequenceEqual(new[] { TicketPriority.High, TicketPriority.Critical })),
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_passes_null_status_when_filter_absent()
    {
        var sut = CreateSut(_customerId);
        await sut.Handle(new ListMyTicketsQuery(Status: null, Priority: null), default);

        await _queries.Received(1).ListForCustomerAsync(
            _customerId, Arg.Any<int>(), Arg.Any<int>(),
            (TicketStatus[]?)null, (TicketPriority[]?)null,
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_forwards_paging_search_and_sort_verbatim()
    {
        var sut = CreateSut(_customerId);
        await sut.Handle(new ListMyTicketsQuery(
            Page: 3, PageSize: 50, Search: "login", SortBy: "priority", SortDirection: "asc"), default);

        await _queries.Received(1).ListForCustomerAsync(
            _customerId, 3, 50,
            Arg.Any<TicketStatus[]?>(), Arg.Any<TicketPriority[]?>(),
            "login", "priority", "asc", Arg.Any<CancellationToken>());
    }
}
