using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Domain.Tickets;
using TicketSystem.Domain.Users;

namespace TicketSystem.Infrastructure.Persistence.Configurations;

public sealed class ActivityTimelineEntryConfiguration : IEntityTypeConfiguration<ActivityTimelineEntry>
{
    public void Configure(EntityTypeBuilder<ActivityTimelineEntry> b)
    {
        b.ToTable("ActivityEntries", t =>
        {
            t.HasCheckConstraint("CK_ActivityEntries_Event", "[Event] IN (1, 2, 3, 4, 5, 6)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Event).HasConversion<int>();
        b.Property(x => x.Summary).HasMaxLength(1000).IsRequired();
        b.Property(x => x.At).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), 0)");

        b.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.TicketId);
        b.HasIndex(x => new { x.TicketId, x.At });
    }
}
