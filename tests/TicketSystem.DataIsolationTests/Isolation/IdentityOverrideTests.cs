using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TicketSystem.DataIsolationTests.Infrastructure;
using TicketSystem.Infrastructure.Persistence;

namespace TicketSystem.DataIsolationTests.Isolation;

[Collection("Isolation")]
public class IdentityOverrideTests
{
    private readonly IsolationApiFactory _factory;

    public IdentityOverrideTests(IsolationApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Query_string_customerId_override_is_silently_ignored()
    {
        var client = _factory.CreateClient();
        await TestAuth.LoginAsync(client, _factory.Fixture.CustomerA.Email);

        // A tries to list B's tickets via query-string identity override.
        var res = await client.GetAsync(
            $"/api/tickets?customerId={_factory.Fixture.CustomerB.Id}");

        res.StatusCode.Should().Be(HttpStatusCode.OK,
            "FR24: override attempt must be silently ignored, not 400");

        var body = await res.Content.ReadAsStringAsync();
        // None of B's sentinel titles should appear.
        body.Should().NotContain("SECRET_B_TITLE_A");
        body.Should().NotContain("SECRET_B_TITLE_B");
        body.Should().NotContain("SECRET_B_TITLE_RESOLVED");
    }

    [Fact]
    public async Task Body_customer_id_on_create_is_ignored()
    {
        var client = _factory.CreateClient();
        await TestAuth.LoginAsync(client, _factory.Fixture.CustomerA.Email);

        // Extra property in the JSON body — System.Text.Json ignores unknown properties
        // by default, but the ambush test asserts the persisted row belongs to A.
        var res = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "ambush",
            description = "body-injected customerId attempt",
            priority = "Medium",
            customerId = _factory.Fixture.CustomerB.Id,   // extra property
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var ticket = await res.Content.ReadFromJsonAsync<CreatedTicket>();
        ticket!.CustomerId.Should().Be(_factory.Fixture.CustomerA.Id,
            "FR24 defense-in-depth: caller identity from JWT, not body");

        // Confirm the DB row matches — belt-and-braces on the DTO check.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Tickets.FindAsync(ticket.Id);
        row!.CustomerId.Should().Be(_factory.Fixture.CustomerA.Id);
    }

    private sealed record CreatedTicket(Guid Id, Guid CustomerId);
}
