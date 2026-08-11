using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Domain.Users;

namespace TicketSystem.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash)
            .HasMaxLength(64)          // hex SHA-256 is exactly 64 chars
            .IsRequired();

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => new { t.UserId, t.RevokedAt });

        // RowVersion — optimistic concurrency guardrail for Story 2.4's rotation.
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);   // preserve audit history even if user is removed

        // Self-FK for the rotation chain — no cascade.
        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(t => t.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);
    }
}
