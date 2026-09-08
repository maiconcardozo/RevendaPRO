using Foundation.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Infrastructure.Persistence.Mappings
{
    public class CustomerMap : EntityMap<Customer>, IEntityTypeConfiguration<Customer>
    {
        public override void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.ToTable("Customer");

            base.Configure(builder);

            builder.Property(e => e.Name).IsRequired().HasMaxLength(120);
            builder.Property(e => e.Document).HasMaxLength(14);
            builder.Property(e => e.Phone).HasMaxLength(20);
            builder.Property(e => e.Email).HasMaxLength(160);
            builder.Property(e => e.Address).HasMaxLength(240);
            builder.Property(e => e.Notes).HasMaxLength(500);

            // Documento e telefone são por onde a proposta acha quem já existe; nome é por onde
            // a lista ordena. Índice único em nenhum deles: o cliente excluído mantém a linha, e
            // um índice recusaria um CPF que voltou — a consulta confere, com IsActive.
            builder.HasIndex(e => new { e.IdTenant, e.Name });
            builder.HasIndex(e => new { e.IdTenant, e.Document });
            builder.HasIndex(e => new { e.IdTenant, e.Phone });

            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.IdTenant)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
