using Foundation.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Infrastructure.Persistence.Mappings
{
    public class SupplierSegmentMap : EntityMap<SupplierSegment>, IEntityTypeConfiguration<SupplierSegment>
    {
        public override void Configure(EntityTypeBuilder<SupplierSegment> builder)
        {
            builder.ToTable("SupplierSegment");

            base.Configure(builder);

            builder.Property(e => e.Name).IsRequired().HasMaxLength(80);

            builder.HasIndex(e => new { e.IdTenant, e.Position });

            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.IdTenant)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class SupplierMap : EntityMap<Supplier>, IEntityTypeConfiguration<Supplier>
    {
        public override void Configure(EntityTypeBuilder<Supplier> builder)
        {
            builder.ToTable("Supplier");

            base.Configure(builder);

            builder.Property(e => e.Name).IsRequired().HasMaxLength(120);
            builder.Property(e => e.ContactName).HasMaxLength(120);
            builder.Property(e => e.ContactPhone).HasMaxLength(20);
            builder.Property(e => e.Document).HasMaxLength(14);
            builder.Property(e => e.Notes).HasMaxLength(500);

            builder.HasIndex(e => new { e.IdTenant, e.Name });
            builder.HasIndex(e => e.IdSupplierSegment);

            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.IdTenant)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict: apagar um ramo jamais leva os fornecedores junto. A regra de negócio
            // recusa antes, e a FK é a rede se ela falhar.
            builder.HasOne<SupplierSegment>()
                .WithMany()
                .HasForeignKey(e => e.IdSupplierSegment)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
