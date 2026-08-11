using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using TicketSystem.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace TicketSystem.IntegrationTests;

/// <summary>
/// WebApplicationFactory that spins up a real SQL Server 2022 container via Testcontainers
/// and points the API's DbContext at it. Shared across every integration test class via
/// <see cref="ApiCollection"/>. Provides <see cref="ResetDatabaseAsync"/> for isolation.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private Respawner? _respawner;

    public string ConnectionString => _sql.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        // Trigger the app boot so migrations run before Respawner tries to read the schema.
        _ = CreateClient();
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
        // Dispose the WebApplicationFactory FIRST (stops the host + drains DbContexts),
        // then stop the SQL container. Reverse order causes teardown SqlExceptions.
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }

    /// <summary>
    /// Wipes all rows from user-defined tables. Call in the constructor of tests that
    /// need a clean DB. Migrations table is preserved so we don't re-run migrations.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null) return;
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                // Any test-only overrides go here. Jwt:SigningKey is set in Story 2.6-ready tests.
                ["Jwt:SigningKey"] = "dGVzdC1qd3Qtc2lnbmluZy1rZXktZm9yLWludGVncmF0aW9uLXRlc3RzLTMyLWJ5dGVz", // base64, 32+ bytes
                ["Jwt:Issuer"] = "ticket-system-tests",
                ["Jwt:Audience"] = "ticket-system-tests",
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
