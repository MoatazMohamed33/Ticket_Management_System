using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Infrastructure.Persistence;
using TicketSystem.Infrastructure.Persistence.Seeding;

namespace TicketSystem.IntegrationTests.Seeding;

[Collection("Api")]
public class SeederIdempotencyTests
{
    private readonly ApiFactory _factory;

    public SeederIdempotencyTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Seed_creates_six_users_and_second_run_is_idempotent()
    {
        await _factory.ResetDatabaseAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            ILogger logger = NullLogger.Instance;

            await DatabaseSeeder.SeedAsync(db, hasher, logger);
        }

        long firstRunCount;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            firstRunCount = await db.Users.LongCountAsync();
        }

        firstRunCount.Should().Be(6);

        // Second run should be a no-op — count unchanged.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            ILogger logger = NullLogger.Instance;

            await DatabaseSeeder.SeedAsync(db, hasher, logger);
        }

        long secondRunCount;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            secondRunCount = await db.Users.LongCountAsync();
        }

        secondRunCount.Should().Be(firstRunCount);
    }

    [Fact]
    public async Task Seeded_users_have_distinct_password_hashes_per_user()
    {
        // Regression guard: previous draft reused a single hash across all 6 users, violating
        // the "per-user salt" guarantee. This test fails immediately if we ever revert.
        await _factory.ResetDatabaseAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        ILogger logger = NullLogger.Instance;

        await DatabaseSeeder.SeedAsync(db, hasher, logger);

        var hashes = await db.Users.Select(u => u.PasswordHash).ToListAsync();
        hashes.Should().HaveCount(6);
        hashes.Distinct().Should().HaveCount(6, "each user must have its own salt");
    }

    [Fact]
    public async Task Seeded_admin_password_verifies_against_the_demo_password()
    {
        await _factory.ResetDatabaseAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        ILogger logger = NullLogger.Instance;

        await DatabaseSeeder.SeedAsync(db, hasher, logger);

        var admin = await db.Users.SingleAsync(u => u.EmailNormalized == "ADMIN@DEMO.LOCAL");
        hasher.Verify(DatabaseSeeder.DemoPassword, admin.PasswordHash).Should().BeTrue();
        hasher.Verify("wrong-password", admin.PasswordHash).Should().BeFalse();
    }
}
