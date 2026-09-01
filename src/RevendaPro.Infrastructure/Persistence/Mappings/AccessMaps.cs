using Foundation.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Infrastructure.Persistence.Mappings
{
    /// <summary>
    /// Mappings follow the same pattern as source/Global/Authentication: ToTable first,
    /// then base.Configure, then what belongs to this entity.
    ///
    /// EntityMap comes from Foundation and already maps Id, Code (unique), IsActive and the
    /// audit columns, and ignores the filter helpers of Entity. These classes exist only for
    /// migrations and schema: at runtime the access path is Dapper. See ADR-0003.
    /// </summary>
    public class TenantMap : EntityMap<Tenant>, IEntityTypeConfiguration<Tenant>
    {
        public override void Configure(EntityTypeBuilder<Tenant> builder)
        {
            builder.ToTable("Tenant");

            base.Configure(builder);

            builder.Property(e => e.Name).IsRequired().HasMaxLength(160);
        }
    }

    public class ScreenMap : EntityMap<Screen>, IEntityTypeConfiguration<Screen>
    {
        public override void Configure(EntityTypeBuilder<Screen> builder)
        {
            builder.ToTable("Screen");

            base.Configure(builder);

            builder.Property(e => e.Key).IsRequired().HasMaxLength(60);
            builder.HasIndex(e => e.Key).IsUnique();

            // Menu label, kept in Portuguese: it is text the user reads. See ADR-0003.
            builder.Property(e => e.Name).IsRequired().HasMaxLength(80);

            builder.Property(e => e.Route).IsRequired().HasMaxLength(160);
            builder.Property(e => e.Icon).HasMaxLength(60);
            builder.Property(e => e.MenuGroup).HasMaxLength(60);
            builder.Property(e => e.Order).IsRequired();
            builder.Property(e => e.ShowInMenu).IsRequired();

            builder.HasIndex(e => new { e.MenuGroup, e.Order });

            builder.HasOne<Screen>()
                .WithMany()
                .HasForeignKey(e => e.IdParentScreen)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class RoleMap : EntityMap<Role>, IEntityTypeConfiguration<Role>
    {
        public override void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.ToTable("Role");

            base.Configure(builder);

            builder.Property(e => e.Name).IsRequired().HasMaxLength(80);
            builder.Property(e => e.Description).HasMaxLength(240);
            builder.Property(e => e.IsSystem).IsRequired();

            builder.HasIndex(e => new { e.IdTenant, e.Name }).IsUnique();

            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.IdTenant)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class RoleScreenMap : EntityMap<RoleScreen>, IEntityTypeConfiguration<RoleScreen>
    {
        public override void Configure(EntityTypeBuilder<RoleScreen> builder)
        {
            builder.ToTable("RoleScreen");

            base.Configure(builder);

            // Unique so the grant statement can rely on ON DUPLICATE KEY UPDATE to
            // reactivate a previous link instead of creating a second row.
            builder.HasIndex(e => new { e.IdRole, e.IdScreen }).IsUnique();

            builder.HasOne<Role>().WithMany().HasForeignKey(e => e.IdRole)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<Screen>().WithMany().HasForeignKey(e => e.IdScreen)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class UserRoleMap : EntityMap<UserRole>, IEntityTypeConfiguration<UserRole>
    {
        public override void Configure(EntityTypeBuilder<UserRole> builder)
        {
            builder.ToTable("UserRole");

            base.Configure(builder);

            builder.HasIndex(e => new { e.IdUser, e.IdRole }).IsUnique();

            builder.HasOne<User>().WithMany().HasForeignKey(e => e.IdUser)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<Role>().WithMany().HasForeignKey(e => e.IdRole)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
