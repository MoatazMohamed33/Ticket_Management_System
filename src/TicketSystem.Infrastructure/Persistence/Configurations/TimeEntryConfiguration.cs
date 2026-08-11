using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Domain.Tickets;
using TicketSystem.Domain.Users;

namespace TicketSystem.Infrastructure.Persistence.Configurations;

public sealed class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> b)
    {
        b.ToTable("TimeEntries", t =>
        {
            t.HasCheckConstraint("CK_TimeEntries_Duration",
                "[DurationMinutes] > 0 AND [DurationMinutes] <= 1440");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.WorkedOn).HasColumnType("date");
        b.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), 0)");

        b.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.TicketId);
        b.HasIndex(x => new { x.TicketId, x.WorkedOn });
    }
}
