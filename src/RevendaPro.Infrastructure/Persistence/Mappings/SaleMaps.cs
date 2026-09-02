using Foundation.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Infrastructure.Persistence.Mappings
{
    public class ProposalMap : EntityMap<Proposal>, IEntityTypeConfiguration<Proposal>
    {
        public override void Configure(EntityTypeBuilder<Proposal> builder)
        {
            builder.ToTable("Proposal");

            base.Configure(builder);

            builder.Property(e => e.ProspectName).IsRequired().HasMaxLength(120);
            builder.Property(e => e.ProspectPhone).HasMaxLength(20);
            builder.Property(e => e.Notes).HasMaxLength(500);

            // Dinheiro em decimal, jamais em ponto flutuante (RNF-12).
            builder.Property(e => e.Amount).HasPrecision(12, 2);
            builder.Property(e => e.PartnerCutPercent).HasPrecision(5, 2);
            builder.Property(e => e.PartnerCutAmount).HasPrecision(12, 2);

            builder.HasIndex(e => new { e.IdVehicle, e.Status });

            builder.HasOne<Vehicle>()
                .WithMany()
                .HasForeignKey(e => e.IdVehicle)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class SaleMap : EntityMap<Sale>, IEntityTypeConfiguration<Sale>
    {
        public override void Configure(EntityTypeBuilder<Sale> builder)
        {
            builder.ToTable("Sale");

            base.Configure(builder);

            builder.Property(e => e.PartnerStoreName).HasMaxLength(120);
            builder.Property(e => e.CommissionNotes).HasMaxLength(200);
            builder.Property(e => e.BuyerName).IsRequired().HasMaxLength(120);
            builder.Property(e => e.BuyerDocument).HasMaxLength(14);
            builder.Property(e => e.BuyerPhone).HasMaxLength(20);
            builder.Property(e => e.Notes).HasMaxLength(500);

            builder.Property(e => e.Amount).HasPrecision(12, 2);
            builder.Property(e => e.PartnerCutPercent).HasPrecision(5, 2);
            builder.Property(e => e.PartnerCutAmount).HasPrecision(12, 2);
            builder.Property(e => e.Commission).HasPrecision(12, 2);
            builder.Property(e => e.TradeInValue).HasPrecision(12, 2);

            // Um carro vende uma vez. A venda cancelada fica excluida logicamente e continua na
            // tabela, entao a unicidade entre as ativas e garantida pela consulta, com teste -
            // pelo mesmo motivo da placa. Ver VehicleMap.
            builder.HasIndex(e => e.IdVehicle);
            builder.HasIndex(e => e.Date);

            builder.HasOne<Vehicle>()
                .WithMany()
                .HasForeignKey(e => e.IdVehicle)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<Proposal>()
                .WithMany()
                .HasForeignKey(e => e.IdProposal)
                .OnDelete(DeleteBehavior.Restrict);

            // O carro que entrou existe por conta propria no patio. Apagar a venda jamais o
            // leva junto, e apaga-lo jamais apaga a venda: os dois so se apontam.
            builder.HasOne<Vehicle>()
                .WithMany()
                .HasForeignKey(e => e.IdTradeInVehicle)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
