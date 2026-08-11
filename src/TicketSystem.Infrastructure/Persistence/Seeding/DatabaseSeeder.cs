using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Domain.Tickets;
using TicketSystem.Domain.Users;

namespace TicketSystem.Infrastructure.Persistence.Seeding;

/// <summary>
/// Development-only seed routine. Users and tickets each have independent idempotency
/// guards so an existing dev DB from earlier epics still picks up new seed rows on the
/// next startup without wiping.
/// </summary>
public static class DatabaseSeeder
{
    public const string DemoPassword = "Passw0rd!";

    public static async Task SeedAsync(
        AppDbContext db,
        IPasswordHasher hasher,
        ILogger logger,
        CancellationToken ct = default)
    {
        var usersExisted = await db.Users.AnyAsync(ct);
        if (!usersExisted)
        {
            await SeedUsersAsync(db, hasher, logger, ct);
        }

        var ticketsExisted = await db.Tickets.AnyAsync(ct);
        if (!ticketsExisted)
        {
            await SeedTicketsAsync(db, logger, ct);
        }

        // Catch-up idempotency — an older seed run may have created tickets before Story 6.4
        // introduced the "close one to populate dashboard resolution metric" step. Ensure at
        // least one Closed ticket exists so averageResolutionMinutes is never null in dev.
        await EnsureOneClosedTicketAsync(db, logger, ct);
    }

    private static async Task EnsureOneClosedTicketAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var hasClosed = await db.Tickets.AnyAsync(t => t.Status == TicketStatus.Closed, ct);
        if (hasClosed) return;

        var toCloseId = await db.Tickets.AsNoTracking()
            .Where(t => t.Status == TicketStatus.Resolved)
            .OrderBy(t => t.CreatedAt)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);
        if (toCloseId is null) return;

        var syntheticClosedAt = DateTimeOffset.UtcNow.AddHours(-2);
        await db.Tickets
            .Where(t => t.Id == toCloseId.Value)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TicketStatus.Closed)
                .SetProperty(t => t.ClosedAt, (DateTimeOffset?)syntheticClosedAt), ct);

        logger.LogInformation("Catch-up seed: closed one Resolved ticket for dashboard resolution metric.");
    }

    private static async Task SeedUsersAsync(AppDbContext db, IPasswordHasher hasher, ILogger logger, CancellationToken ct)
    {
        // CRITICAL: hash per user so each row gets its own salt (satisfies AC-4 "per-user salt").
        // Reusing a single Hash() call would produce byte-identical PasswordHash rows.
        string HashDemo() => hasher.Hash(DemoPassword);

        var seedUsers = new[]
        {
            User.CreateStaff("admin@demo.local",         "Demo Admin",      UserRole.Admin,        HashDemo()),
            User.CreateStaff("agent1@demo.local",        "Agent One",       UserRole.SupportAgent, HashDemo()),
            User.CreateStaff("agent2@demo.local",        "Agent Two",       UserRole.SupportAgent, HashDemo()),
            User.CreateCustomer("customer1@demo.local",  "Customer One",    HashDemo()),
            User.CreateCustomer("customer2@demo.local",  "Customer Two",    HashDemo()),
            User.CreateCustomer("customer3@demo.local",  "Customer Three",  HashDemo()),
        };

        db.Users.AddRange(seedUsers);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Seeded {Count} users. Login as admin@demo.local / {Password} (also agent1..2, customer1..3).",
            seedUsers.Length, DemoPassword);
    }

    private static async Task SeedTicketsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        // Resolve customer ids from the just-seeded users.
        var customers = await db.Users
            .Where(u => u.Role == UserRole.Customer)
            .OrderBy(u => u.Email)
            .ToListAsync(ct);
        if (customers.Count == 0)
        {
            logger.LogWarning("Ticket seed skipped — no customer users present.");
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // Spread ownership across customers so Story 4.6 isolation tests have real cross-owner data.
        // Coverage: at least one ticket per priority; mix of statuses (Open is set by the factory,
        // other statuses are applied via later mutation stories — 4.5, Epic 5. For now everything
        // seeds as Open; Story 4.5 seeder extension will bump one per customer to Resolved).
        var titles = new[]
        {
            ("Cannot log in", "Password reset link never arrived.", TicketPriority.High),
            ("Feature request: dark mode", "Would love a dark theme option.", TicketPriority.Low),
            ("Billing discrepancy", "Invoice #4487 shows the wrong amount.", TicketPriority.Medium),
            ("Site is down", "Every page returns 502.", TicketPriority.Critical),
            ("Export to CSV fails", "Export button spins forever on large datasets.", TicketPriority.Medium),
            ("Typo in confirmation email", "Says 'you're' where it should be 'your'.", TicketPriority.Low),
        };

        var tickets = new List<Ticket>();
        for (var i = 0; i < titles.Length; i++)
        {
            var (title, desc, prio) = titles[i];
            var owner = customers[i % customers.Count];
            tickets.Add(Ticket.Create(title, desc, prio, owner.Id, now));
        }

        db.Tickets.AddRange(tickets);
        await db.SaveChangesAsync(ct);

        // ORDER MATTERS — agent assignment must run BEFORE the Resolved/Closed bumps
        // so it sees a pool of Open+unassigned tickets. Bumps come after, potentially
        // closing tickets that were previously assigned (fine — Closed-and-assigned is a
        // legitimate historical state).

        // Story 5.1/5.2 — assign currently-Open+unassigned tickets round-robin across
        // agents, then flip one Open ticket per agent to InProgress.
        var agents = await db.Users
            .Where(u => u.Role == UserRole.SupportAgent)
            .OrderBy(u => u.Email)
            .ToListAsync(ct);
        if (agents.Count >= 1)
        {
            var openIds = await db.Tickets.AsNoTracking()
                .Where(t => t.Status == TicketStatus.Open && t.AssignedAgentId == null)
                .OrderBy(t => t.CreatedAt)
                .Select(t => t.Id)
                .Take(agents.Count * 2)   // spread ~2 per agent
                .ToListAsync(ct);

            for (var i = 0; i < openIds.Count; i++)
            {
                var agent = agents[i % agents.Count];
                var targetId = openIds[i];
                await db.Tickets
                    .Where(t => t.Id == targetId)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.AssignedAgentId, (Guid?)agent.Id), ct);
            }

            // Flip one Open+assigned ticket per agent to InProgress.
            foreach (var agent in agents)
            {
                var toBump = await db.Tickets.AsNoTracking()
                    .Where(t => t.AssignedAgentId == agent.Id && t.Status == TicketStatus.Open)
                    .Select(t => (Guid?)t.Id)
                    .FirstOrDefaultAsync(ct);
                if (toBump is null) continue;
                await db.Tickets
                    .Where(t => t.Id == toBump.Value)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.InProgress), ct);
            }
        }

        // Bump one ticket per customer to Resolved so Story 4.5 close-flow has seed data.
        // Picked from tickets that are still Open (skips ones just moved to InProgress by
        // the agent assignment above). Ensures the Resolved-flip is on a legit target.
        foreach (var owner in customers)
        {
            var toResolveId = await db.Tickets.AsNoTracking()
                .Where(t => t.CustomerId == owner.Id && t.Status == TicketStatus.Open)
                .OrderBy(t => t.CreatedAt)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(ct);
            if (toResolveId is null) continue;
            await db.Tickets
                .Where(t => t.Id == toResolveId.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.Resolved), ct);
        }

        // Story 6.4 — close ONE ticket (across the whole seed, not per-customer) so
        // averageResolutionMinutes is non-null in dev. Pick a just-Resolved one and set
        // a synthetic ClosedAt so the dashboard shows a realistic resolution time.
        var toCloseId = await db.Tickets.AsNoTracking()
            .Where(t => t.Status == TicketStatus.Resolved)
            .OrderBy(t => t.CreatedAt)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);
        if (toCloseId is not null)
        {
            var syntheticClosedAt = now.AddHours(-2);   // resolved-then-closed 2 hours ago
            await db.Tickets
                .Where(t => t.Id == toCloseId.Value)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, TicketStatus.Closed)
                    .SetProperty(t => t.ClosedAt, (DateTimeOffset?)syntheticClosedAt), ct);
        }

        logger.LogInformation(
            "Seeded {Count} tickets across {CustomerCount} customers (one Resolved per customer).",
            tickets.Count, customers.Count);
    }
}
