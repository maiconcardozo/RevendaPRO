using Foundation.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Infrastructure.Persistence.Mappings
{
    public class YardMap : EntityMap<Yard>, IEntityTypeConfiguration<Yard>
    {
        public override void Configure(EntityTypeBuilder<Yard> builder)
        {
            builder.ToTable("Yard");

            base.Configure(builder);

            builder.Property(e => e.Name).IsRequired().HasMaxLength(80);
            builder.Property(e => e.Kind).IsRequired();
            builder.Property(e => e.ContactName).HasMaxLength(120);
            builder.Property(e => e.ContactPhone).HasMaxLength(20);
            builder.Property(e => e.Notes).HasMaxLength(500);

            // Dinheiro em decimal, jamais em ponto flutuante (RNF-12).
            builder.Property(e => e.CutPercent).HasPrecision(5, 2);
            builder.Property(e => e.CutAmount).HasPrecision(12, 2);

            builder.HasIndex(e => new { e.IdTenant, e.Position });

            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.IdTenant)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
