using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Domain.Users;

namespace TicketSystem.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", t =>
        {
            // FR10 — single-role invariant enforced at DB level.
            t.HasCheckConstraint("CK_Users_Role", "[Role] IN (1, 2, 3)");
        });

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(u => u.EmailNormalized)
            .HasMaxLength(320)
            .IsRequired();

        // Lookups always go through EmailNormalized — index there, not on Email.
        builder.HasIndex(u => u.EmailNormalized).IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(u => u.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasConversion<int>();

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);

        builder.Property(u => u.CreatedAt)
            // SQL Server has SYSDATETIMEOFFSET() (returns datetimeoffset with the server's
            // local TZ offset) and SYSUTCDATETIME() (returns datetime2 in UTC, no offset).
            // We want a datetimeoffset in UTC regardless of where SQL Server runs — combine
            // via TODATETIMEOFFSET so the DB default is correct on any host timezone.
            .HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), 0)");

        builder.Property(u => u.DeletedAt)
            .IsRequired(false);
    }
}
