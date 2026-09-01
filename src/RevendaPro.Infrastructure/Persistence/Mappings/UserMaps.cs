using Foundation.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Infrastructure.Persistence.Mappings
{
    public class UserMap : EntityMap<User>, IEntityTypeConfiguration<User>
    {
        public override void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("User");

            base.Configure(builder);

            builder.Property(e => e.Name).IsRequired().HasMaxLength(160);
            builder.Property(e => e.Email).IsRequired().HasMaxLength(180);
            builder.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            builder.Property(e => e.Photo).HasMaxLength(80);
            builder.Property(e => e.Document).HasMaxLength(14);
            builder.Property(e => e.Phone).HasMaxLength(11);

            builder.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();

            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class RefreshTokenMap : EntityMap<RefreshToken>, IEntityTypeConfiguration<RefreshToken>
    {
        public override void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("RefreshToken");

            base.Configure(builder);

            builder.Property(e => e.TokenHash).IsRequired().HasMaxLength(255);
            builder.Property(e => e.ExpiresAt).IsRequired();

            builder.HasIndex(e => e.TokenHash);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class AuditLogMap : EntityMap<AuditLog>, IEntityTypeConfiguration<AuditLog>
    {
        public override void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("AuditLog");

            base.Configure(builder);

            builder.Property(e => e.EntityName).IsRequired().HasMaxLength(80);
            builder.Property(e => e.RecordCode).IsRequired();
            builder.Property(e => e.Action).HasConversion<int>().IsRequired();
            builder.Property(e => e.OldValues).HasColumnType("json");
            builder.Property(e => e.NewValues).HasColumnType("json");

            builder.HasIndex(e => new { e.EntityName, e.RecordCode });
        }
    }
}
