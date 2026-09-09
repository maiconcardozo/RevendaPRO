using Foundation.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Infrastructure.Persistence.Mappings
{
    public class StoreExpenseMap : EntityMap<StoreExpense>, IEntityTypeConfiguration<StoreExpense>
    {
        public override void Configure(EntityTypeBuilder<StoreExpense> builder)
        {
            builder.ToTable("StoreExpense");

            base.Configure(builder);

            builder.Property(e => e.Description).IsRequired().HasMaxLength(160);
            builder.Property(e => e.Notes).HasMaxLength(1000);

            // Dinheiro em decimal, jamais em ponto flutuante (RNF-12).
            builder.Property(e => e.Amount).HasPrecision(12, 2);

            builder.HasIndex(e => new { e.IdTenant, e.Date });
            builder.HasIndex(e => e.IdExpenseType);
            builder.HasIndex(e => e.IdSupplier);

            // O que vence, e o que já venceu: a mesma pergunta do gasto do carro, e o mesmo índice.
            builder.HasIndex(e => new { e.IsPaid, e.DueDate });

            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.IdTenant)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict nos dois: apagar um tipo de gasto ou um fornecedor jamais leva junto as
            // despesas que apontam para ele. A regra de negócio recusa antes, e a FK é a rede.
            builder.HasOne<ExpenseType>()
                .WithMany()
                .HasForeignKey(e => e.IdExpenseType)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Supplier>()
                .WithMany()
                .HasForeignKey(e => e.IdSupplier)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class SaleReceiptMap : EntityMap<SaleReceipt>, IEntityTypeConfiguration<SaleReceipt>
    {
        public override void Configure(EntityTypeBuilder<SaleReceipt> builder)
        {
            builder.ToTable("SaleReceipt");

            base.Configure(builder);

            builder.Property(e => e.Notes).HasMaxLength(500);
            builder.Property(e => e.Amount).HasPrecision(12, 2);

            builder.HasIndex(e => e.IdSale);
            builder.HasIndex(e => e.Date);

            // Cascade: a entrada existe pela venda, e cancelar a venda leva o que entrou por ela.
            // É o oposto do carro da troca, que existe por conta própria e fica no pátio.
            builder.HasOne<Sale>()
                .WithMany()
                .HasForeignKey(e => e.IdSale)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
