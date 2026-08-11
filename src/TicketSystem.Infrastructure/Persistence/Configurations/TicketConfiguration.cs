using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Domain.Tickets;
using TicketSystem.Domain.Users;

namespace TicketSystem.Infrastructure.Persistence.Configurations;

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.ToTable("Tickets", t =>
        {
            t.HasCheckConstraint("CK_Tickets_Status",   "[Status]   IN (1, 2, 3, 4)");
            t.HasCheckConstraint("CK_Tickets_Priority", "[Priority] IN (1, 2, 3, 4)");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(8000).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Priority).HasConversion<int>();

        // FR20 — optimistic-concurrency token via SQL Server rowversion.
        b.Property(x => x.RowVersion).IsRowVersion();

        // datetimeoffset in UTC regardless of host TZ — same pattern as UserConfiguration.
        b.Property(x => x.CreatedAt).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), 0)");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), 0)");
        b.Property(x => x.ClosedAt).IsRequired(false);   // Story 6.4 — set on transition to Closed

        // FR21 preservation — Restrict cascade so Story 3.3 deactivation keeps historical tickets.
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for list-query paths (FR12/13/14, NFR-C2 at 50k rows).
        b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.Priority);
        b.HasIndex(x => x.AssignedAgentId);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => x.UpdatedAt);
    }
}
