using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TicketSystem.DataIsolationTests.Infrastructure;

namespace TicketSystem.DataIsolationTests.Isolation;

[Collection("Isolation")]
public class CrossCustomerAccessTests
{
    private readonly IsolationApiFactory _factory;

    public CrossCustomerAccessTests(IsolationApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_getting_B_ticket_detail_returns_404_no_leak()
    {
        var client = _factory.CreateClient();
        await TestAuth.LoginAsync(client, _factory.Fixture.CustomerA.Email);

        var target = _factory.Fixture.BTickets[0];
        var res = await client.GetAsync($"/api/tickets/{target.Id}");

        await IsolationAssertions.AssertNoLeakageAsync(res, target);
    }

    [Fact]
    public async Task A_posting_comment_to_B_ticket_returns_404_no_leak()
    {
        var client = _factory.CreateClient();
        await TestAuth.LoginAsync(client, _factory.Fixture.CustomerA.Email);

        var target = _factory.Fixture.BTickets[0];
        var res = await client.PostAsJsonAsync($"/api/tickets/{target.Id}/comments",
            new { body = "attempt" });

        await IsolationAssertions.AssertNoLeakageAsync(res, target);
    }

    [Fact]
    public async Task A_closing_B_resolved_ticket_returns_404_no_leak()
    {
        var client = _factory.CreateClient();
        await TestAuth.LoginAsync(client, _factory.Fixture.CustomerA.Email);

        var target = _factory.Fixture.BResolvedTicket;
        var res = await client.PostAsJsonAsync($"/api/tickets/{target.Id}/close",
            new { rowVersion = Convert.ToBase64String(target.RowVersion) });

        await IsolationAssertions.AssertNoLeakageAsync(res, target);
    }

    [Fact]
    public async Task Nonexistent_and_forbidden_return_indistinguishable_shape()
    {
        var client = _factory.CreateClient();
        await TestAuth.LoginAsync(client, _factory.Fixture.CustomerA.Email);

        var forbidden = _factory.Fixture.BTickets[0];
        var random = Guid.NewGuid();

        var forbiddenRes = await client.GetAsync($"/api/tickets/{forbidden.Id}");
        var randomRes    = await client.GetAsync($"/api/tickets/{random}");

        forbiddenRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
        randomRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Compare the top-level ProblemDetails shape (title + detail + type). CorrelationId
        // naturally differs per request; strip it before comparing.
        var forbiddenBody = await NormalizeAsync(forbiddenRes);
        var randomBody    = await NormalizeAsync(randomRes);
        forbiddenBody.Should().Be(randomBody,
            "attacker cannot distinguish 'ticket exists but not yours' from 'ticket does not exist'");

        static async Task<string> NormalizeAsync(HttpResponseMessage r)
        {
            var s = await r.Content.ReadAsStringAsync();
            // Strip the correlationId key + its value (guid) so the two bodies compare equal.
            return System.Text.RegularExpressions.Regex.Replace(
                s, "\"correlationId\"\\s*:\\s*\"[^\"]*\"", "\"correlationId\":\"redacted\"");
        }
    }
}
