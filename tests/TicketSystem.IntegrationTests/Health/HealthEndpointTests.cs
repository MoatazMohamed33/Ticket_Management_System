using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace TicketSystem.IntegrationTests.Health;

[Collection("Api")]
public class HealthEndpointTests
{
    private readonly ApiFactory _factory;

    public HealthEndpointTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_health_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("X-Correlation-Id");
    }

    [Fact]
    public async Task Get_deep_health_returns_ok_and_reports_db_reachable()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health/deep");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<HealthDetailShape>();
        body.Should().NotBeNull();
        body!.DatabaseReachable.Should().BeTrue("Testcontainer SQL Server is healthy at this point");
        body.DbLatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }

    private sealed record HealthDetailShape(string ApiVersion, bool DatabaseReachable, long DbLatencyMs, DateTimeOffset UtcNow);
}
