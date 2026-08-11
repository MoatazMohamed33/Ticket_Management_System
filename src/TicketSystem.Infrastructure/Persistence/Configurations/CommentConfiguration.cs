using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Domain.Tickets;
using TicketSystem.Domain.Users;

namespace TicketSystem.Infrastructure.Persistence.Configurations;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b)
    {
        b.ToTable("Comments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), 0)");

        b.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.TicketId);
        b.HasIndex(x => new { x.TicketId, x.CreatedAt });
    }
}
