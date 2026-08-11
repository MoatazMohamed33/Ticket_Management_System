using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Testcontainers.MsSql;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Domain.Tickets;
using TicketSystem.Domain.Users;
using TicketSystem.Infrastructure.Persistence;

namespace TicketSystem.DataIsolationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory dedicated to the isolation adversary suite. Ships its own
/// deterministic seed (two Customers A/B plus tickets owned by B whose title/description
/// the suite asserts are NEVER echoed back to A). Uses a separate MSSQL container from
/// TicketSystem.IntegrationTests so the two suites don't share state.
/// </summary>
public class IsolationApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private Respawner? _respawner;

    public string ConnectionString => _sql.GetConnectionString();

    // JWT signing key used by the test API + emitted tokens. base64, 32+ bytes.
    public const string SigningKeyBase64 =
        "aXNvbGF0aW9uLXRlc3RzLXNpZ25pbmcta2V5LTMyLWJ5dGVzLW1pbmltdW0h";

    public IsolationFixture Fixture { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        _ = CreateClient();  // boots host → runs migrations
        Fixture = await SeedAsync();

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = new[] { "dbo" },
            TablesToIgnore = new[]
            {
                new Respawn.Graph.Table("__EFMigrationsHistory"),
            },
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }

    /// <summary>Re-seed after a Respawn wipe. Tests that mutate state should call this.</summary>
    public async Task ResetAndReseedAsync()
    {
        if (_respawner is null) return;
        await using (var conn = new SqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            await _respawner.ResetAsync(conn);
        }
        Fixture = await SeedAsync();
    }

    private async Task<IsolationFixture> SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        string Hash() => hasher.Hash("Passw0rd!");

        var customerA = User.CreateCustomer("isolation-a@test.local", "Customer A", Hash());
        var customerB = User.CreateCustomer("isolation-b@test.local", "Customer B", Hash());
        var customerC = User.CreateCustomer("isolation-c@test.local", "Customer C", Hash());
        var admin     = User.CreateStaff("isolation-admin@test.local", "Iso Admin", UserRole.Admin, Hash());
        var agent1    = User.CreateStaff("isolation-agent1@test.local", "Iso Agent 1", UserRole.SupportAgent, Hash());
        var agent2    = User.CreateStaff("isolation-agent2@test.local", "Iso Agent 2", UserRole.SupportAgent, Hash());

        db.Users.AddRange(customerA, customerB, customerC, admin, agent1, agent2);
        await db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;

        var aTicket1 = Ticket.Create("A private issue 1", "A description 1", TicketPriority.Low, customerA.Id, now);
        var aTicket2 = Ticket.Create("A private issue 2", "A description 2", TicketPriority.Medium, customerA.Id, now);

        // The B-tickets carry sentinel strings whose absence is asserted in every A-response body.
        var bTicket1 = Ticket.Create("SECRET_B_TITLE_A", "SECRET_B_DESC_A", TicketPriority.High, customerB.Id, now);
        var bTicket2 = Ticket.Create("SECRET_B_TITLE_B", "SECRET_B_DESC_B", TicketPriority.Critical, customerB.Id, now);
        var bTicketResolved = Ticket.Create("SECRET_B_TITLE_RESOLVED", "SECRET_B_DESC_RESOLVED", TicketPriority.Medium, customerB.Id, now);

        var cTicket1 = Ticket.Create("C ticket", "C description", TicketPriority.Low, customerC.Id, now);

        db.Tickets.AddRange(aTicket1, aTicket2, bTicket1, bTicket2, bTicketResolved, cTicket1);
        await db.SaveChangesAsync();

        // Flip one B ticket to Resolved so the "A closes B's ticket" test has a plausible target.
        await db.Tickets.Where(t => t.Id == bTicketResolved.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.Resolved));

        // Reload the resolved ticket to capture the updated RowVersion.
        var reloadedResolved = await db.Tickets.AsNoTracking()
            .FirstAsync(t => t.Id == bTicketResolved.Id);

        return new IsolationFixture(
            CustomerA: customerA, CustomerB: customerB, CustomerC: customerC,
            Admin: admin, Agent1: agent1, Agent2: agent2,
            ATickets: new[] { aTicket1, aTicket2 },
            BTickets: new[] { bTicket1, bTicket2, reloadedResolved },
            BResolvedTicket: reloadedResolved);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["Jwt:SigningKey"] = SigningKeyBase64,
                ["Jwt:Issuer"] = "isolation-tests",
                ["Jwt:Audience"] = "isolation-tests",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(o => o.UseSqlServer(ConnectionString));
        });
    }
}

public sealed record IsolationFixture(
    User CustomerA,
    User CustomerB,
    User CustomerC,
    User Admin,
    User Agent1,
    User Agent2,
    Ticket[] ATickets,
    Ticket[] BTickets,
    Ticket BResolvedTicket);
