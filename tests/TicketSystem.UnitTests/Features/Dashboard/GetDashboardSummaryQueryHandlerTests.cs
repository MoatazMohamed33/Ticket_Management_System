using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using TicketSystem.Application.Abstractions.Caching;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Features.Dashboard;
using TicketSystem.Application.Features.Dashboard.Queries.GetDashboardSummary;

namespace TicketSystem.UnitTests.Features.Dashboard;

public class GetDashboardSummaryQueryHandlerTests
{
    private readonly IDashboardQueries _queries = Substitute.For<IDashboardQueries>();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly IDashboardCacheInvalidator _invalidator = Substitute.For<IDashboardCacheInvalidator>();

    private GetDashboardSummaryQueryHandler CreateSut()
    {
        _invalidator.CurrentVersion.Returns(1L);
        _queries.GetSummaryAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(new DashboardSummaryDto(
                new Dictionary<string, int> { ["Open"] = 0 }, 0, null, Array.Empty<AgentWorkloadDto>()));
        return new GetDashboardSummaryQueryHandler(_queries, _cache, _invalidator);
    }

    [Theory]
    [InlineData("7d", 7)]
    [InlineData("30d", 30)]
    [InlineData("90d", 90)]
    [InlineData("365d", 365)]
    [InlineData("30D", 30)]    // case-insensitive
    public async Task Window_valid_Nd_passes_days_to_queries(string raw, int expectedDays)
    {
        var sut = CreateSut();
        await sut.Handle(new GetDashboardSummaryQuery(raw), default);

        await _queries.Received(1).GetSummaryAsync(
            TimeSpan.FromDays(expectedDays), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("all")]
    [InlineData("ALL")]
    [InlineData("All")]
    public async Task Window_all_passes_null_to_queries(string raw)
    {
        var sut = CreateSut();
        await sut.Handle(new GetDashboardSummaryQuery(raw), default);

        await _queries.Received(1).GetSummaryAsync(
            (TimeSpan?)null, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bogus")]
    [InlineData("30")]        // missing 'd'
    [InlineData("d")]         // missing number
    [InlineData("0d")]        // zero rejected
    [InlineData("400d")]      // > 365 rejected
    [InlineData("-7d")]       // negative rejected (regex requires \d+)
    public async Task Window_malformed_or_out_of_range_falls_back_to_30d(string? raw)
    {
        var sut = CreateSut();
        await sut.Handle(new GetDashboardSummaryQuery(raw), default);

        await _queries.Received(1).GetSummaryAsync(
            TimeSpan.FromDays(30), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Second_call_within_TTL_serves_from_cache_and_skips_queries()
    {
        var sut = CreateSut();
        await sut.Handle(new GetDashboardSummaryQuery("30d"), default);
        await sut.Handle(new GetDashboardSummaryQuery("30d"), default);

        await _queries.Received(1).GetSummaryAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Version_bump_makes_prior_cache_entry_unreachable()
    {
        var sut = CreateSut();
        await sut.Handle(new GetDashboardSummaryQuery("30d"), default);

        _invalidator.CurrentVersion.Returns(2L);   // simulate an invalidate between calls
        await sut.Handle(new GetDashboardSummaryQuery("30d"), default);

        await _queries.Received(2).GetSummaryAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Different_windows_have_independent_cache_entries()
    {
        var sut = CreateSut();
        await sut.Handle(new GetDashboardSummaryQuery("30d"), default);
        await sut.Handle(new GetDashboardSummaryQuery("7d"),  default);

        await _queries.Received(1).GetSummaryAsync(TimeSpan.FromDays(30), Arg.Any<CancellationToken>());
        await _queries.Received(1).GetSummaryAsync(TimeSpan.FromDays(7),  Arg.Any<CancellationToken>());
    }
}
