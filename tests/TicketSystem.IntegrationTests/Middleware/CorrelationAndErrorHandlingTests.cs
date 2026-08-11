using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace TicketSystem.IntegrationTests.Middleware;

[Collection("Api")]
public class CorrelationAndErrorHandlingTests
{
    private readonly ApiFactory _factory;

    public CorrelationAndErrorHandlingTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Response_echoes_valid_client_correlation_id()
    {
        var client = _factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        req.Headers.Add("X-Correlation-Id", correlationId);

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Correlation-Id").Single().Should().Be(correlationId);
    }

    [Fact]
    public async Task Response_generates_correlation_id_when_client_sends_malicious_input()
    {
        // Log-injection defense: a non-GUID header must be rejected (server generates a fresh GUID)
        var client = _factory.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        req.Headers.Add("X-Correlation-Id", "injected\nFAKE\r\nMORE");

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var returned = response.Headers.GetValues("X-Correlation-Id").Single();
        Guid.TryParse(returned, out _).Should().BeTrue();
        returned.Should().NotContain("FAKE");
    }

    [Fact]
    public async Task Response_generates_correlation_id_when_client_sends_no_header()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var returned = response.Headers.GetValues("X-Correlation-Id").Single();
        Guid.TryParse(returned, out _).Should().BeTrue();
    }
}
