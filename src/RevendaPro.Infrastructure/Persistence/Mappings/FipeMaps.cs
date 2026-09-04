using Foundation.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Infrastructure.Persistence.Mappings
{
    public class FipeQuoteMap : EntityMap<FipeQuote>, IEntityTypeConfiguration<FipeQuote>
    {
        public override void Configure(EntityTypeBuilder<FipeQuote> builder)
        {
            builder.ToTable("FipeQuote");

            base.Configure(builder);

            builder.Property(e => e.FipeCode).IsRequired().HasMaxLength(10);
            builder.Property(e => e.YearFuel).IsRequired().HasMaxLength(10);
            builder.Property(e => e.ReferenceMonth).IsRequired();
            builder.Property(e => e.Brand).IsRequired().HasMaxLength(60);
            builder.Property(e => e.Model).IsRequired().HasMaxLength(160);

            // Dinheiro em decimal, jamais em ponto flutuante (RNF-12).
            builder.Property(e => e.Value).HasPrecision(12, 2);

            // Unico, ao contrario do que foi feito com a placa. Aqui a exclusao logica nao
            // atrapalha: uma cotacao de mes fechado e fato historico, e o sistema nao tem
            // caminho que apague uma. E a unicidade importa duas vezes - ela e a regra
            // "uma cotacao por modelo e mes" e e o que mantem a leitura de uma linha so.
            builder.HasIndex(e => new { e.FipeCode, e.YearFuel, e.ReferenceMonth }).IsUnique();

            // Sem chave estrangeira para Vehicle de proposito: a cotacao existe por si, e vale
            // para todo carro daquele modelo - inclusive os que ainda nao foram cadastrados.
        }
    }
}
