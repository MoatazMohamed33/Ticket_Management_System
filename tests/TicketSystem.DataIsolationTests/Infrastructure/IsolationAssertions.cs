using System.Net;
using FluentAssertions;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.DataIsolationTests.Infrastructure;

/// <summary>
/// Reusable "response revealed nothing about the forbidden resource" assertion.
/// Used everywhere the suite verifies FR21 no-leak convention on ticket endpoints.
/// </summary>
internal static class IsolationAssertions
{
    public static async Task AssertNoLeakageAsync(HttpResponseMessage res, Ticket forbidden)
    {
        res.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "FR21 requires cross-customer access to return 404, never 403");

        var body = await res.Content.ReadAsStringAsync();

        body.Should().NotContain(forbidden.Title,
            "response leaked forbidden ticket title — potential FR21 violation");
        body.Should().NotContain(forbidden.Description,
            "response leaked forbidden ticket description — potential FR21 violation");
        body.Should().NotContain(forbidden.Id.ToString(),
            "response leaked forbidden ticket id — potential FR21 violation");
        body.Should().NotContain("customerId",
            "response DTO shape includes customerId — potential inference vector");
    }
}
