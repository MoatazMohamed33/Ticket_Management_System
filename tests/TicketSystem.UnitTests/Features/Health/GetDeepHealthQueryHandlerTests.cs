using FluentAssertions;
using NSubstitute;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Features.Health.Queries.GetDeepHealth;

namespace TicketSystem.UnitTests.Features.Health;

public class GetDeepHealthQueryHandlerTests
{
    [Fact]
    public async Task Returns_healthy_when_db_reachable()
    {
        var probe = Substitute.For<IDatabaseProbe>();
        probe.CanConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        var sut = new GetDeepHealthQueryHandler(probe);

        var result = await sut.Handle(new GetDeepHealthQuery(), CancellationToken.None);

        result.DatabaseReachable.Should().BeTrue();
        result.ApiVersion.Should().NotBeNullOrWhiteSpace();
        result.DbLatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Returns_unreachable_when_probe_returns_false()
    {
        var probe = Substitute.For<IDatabaseProbe>();
        probe.CanConnectAsync(Arg.Any<CancellationToken>()).Returns(false);
        var sut = new GetDeepHealthQueryHandler(probe);

        var result = await sut.Handle(new GetDeepHealthQuery(), CancellationToken.None);

        result.DatabaseReachable.Should().BeFalse();
    }
}
