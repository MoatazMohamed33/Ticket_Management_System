using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace TicketSystem.IntegrationTests.Swagger;

[Collection("Api")]
public class SwaggerSpecTests
{
    private readonly ApiFactory _factory;

    public SwaggerSpecTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Swagger_json_is_exposed_and_lists_health_endpoints()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/health", out _).Should().BeTrue("liveness endpoint must be listed");
        paths.TryGetProperty("/api/health/deep", out _).Should().BeTrue("deep health endpoint must be listed");
    }
}
