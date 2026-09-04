using Foundation.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Infrastructure.Persistence.Mappings
{
    public class VehicleMap : EntityMap<Vehicle>, IEntityTypeConfiguration<Vehicle>
    {
        public override void Configure(EntityTypeBuilder<Vehicle> builder)
        {
            builder.ToTable("Vehicle");

            base.Configure(builder);

            builder.Property(e => e.Plate).IsRequired().HasMaxLength(7);
            builder.Property(e => e.Chassis).IsRequired().HasMaxLength(17);
            builder.Property(e => e.Brand).IsRequired().HasMaxLength(60);
            builder.Property(e => e.Model).IsRequired().HasMaxLength(80);
            builder.Property(e => e.Version).HasMaxLength(80);
            builder.Property(e => e.Color).HasMaxLength(30);
            builder.Property(e => e.Renavam).HasMaxLength(11);
            builder.Property(e => e.SupplierName).HasMaxLength(160);
            builder.Property(e => e.DamageDescription).HasMaxLength(500);
            builder.Property(e => e.MarketNotes).HasMaxLength(500);
            builder.Property(e => e.Notes).HasMaxLength(1000);
            builder.Property(e => e.FipeCode).HasMaxLength(10);
            builder.Property(e => e.FipeYearFuel).HasMaxLength(10);
            builder.Property(e => e.FipeSource);

            // Dinheiro em decimal, jamais em ponto flutuante (RNF-12).
            builder.Property(e => e.PurchasePrice).HasPrecision(12, 2);
            builder.Property(e => e.BudgetCeiling).HasPrecision(12, 2);
            builder.Property(e => e.FipeValue).HasPrecision(12, 2);
            builder.Property(e => e.DesiredNetPrice).HasPrecision(12, 2);
            builder.Property(e => e.MinimumNetPrice).HasPrecision(12, 2);
            builder.Property(e => e.AdvertisedPrice).HasPrecision(12, 2);

            // Unicidade por empresa. A exclusao logica fica de fora do indice de proposito:
            // MySQL trata NULL como distinto, e IsActive e booleano, entao um indice unico
            // sobre (IdTenant, Plate, IsActive) permitiria duas placas ativas iguais assim que
            // uma terceira fosse excluida. Quem garante isso e a consulta, com teste.
            builder.HasIndex(e => new { e.IdTenant, e.Plate });
            builder.HasIndex(e => new { e.IdTenant, e.Chassis });
            builder.HasIndex(e => new { e.IdTenant, e.Status });

            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.IdTenant)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class ExpenseTypeMap : EntityMap<ExpenseType>, IEntityTypeConfiguration<ExpenseType>
    {
        public override void Configure(EntityTypeBuilder<ExpenseType> builder)
        {
            builder.ToTable("ExpenseType");

            base.Configure(builder);

            builder.Property(e => e.Name).IsRequired().HasMaxLength(80);
            builder.Property(e => e.Keywords).HasMaxLength(500);

            builder.HasIndex(e => new { e.IdTenant, e.Position });

            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.IdTenant)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class VehicleExpenseMap : EntityMap<VehicleExpense>, IEntityTypeConfiguration<VehicleExpense>
    {
        public override void Configure(EntityTypeBuilder<VehicleExpense> builder)
        {
            builder.ToTable("VehicleExpense");

            base.Configure(builder);

            builder.Property(e => e.Description).IsRequired().HasMaxLength(160);
            builder.Property(e => e.Notes).HasMaxLength(1000);
            builder.Property(e => e.Amount).HasPrecision(12, 2);

            builder.HasIndex(e => e.IdVehicle);
            builder.HasIndex(e => e.IdExpenseType);

            builder.HasOne<Vehicle>()
                .WithMany()
                .HasForeignKey(e => e.IdVehicle)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, e nunca Cascade: apagar um tipo de gasto jamais leva junto os
            // lancamentos que apontam para ele. A regra de negocio recusa a exclusao antes
            // disso, e a FK e a rede que impede o estrago se ela falhar.
            builder.HasOne<ExpenseType>()
                .WithMany()
                .HasForeignKey(e => e.IdExpenseType)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class VehiclePhotoMap : EntityMap<VehiclePhoto>, IEntityTypeConfiguration<VehiclePhoto>
    {
        public override void Configure(EntityTypeBuilder<VehiclePhoto> builder)
        {
            builder.ToTable("VehiclePhoto");

            base.Configure(builder);

            builder.Property(e => e.StorageKey).IsRequired().HasMaxLength(200);
            builder.Property(e => e.ContentType).IsRequired().HasMaxLength(40);

            builder.HasIndex(e => new { e.IdVehicle, e.Position });

            builder.HasOne<Vehicle>()
                .WithMany()
                .HasForeignKey(e => e.IdVehicle)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class VehicleDocumentMap : EntityMap<VehicleDocument>, IEntityTypeConfiguration<VehicleDocument>
    {
        public override void Configure(EntityTypeBuilder<VehicleDocument> builder)
        {
            builder.ToTable("VehicleDocument");

            base.Configure(builder);

            builder.Property(e => e.StorageKey).IsRequired().HasMaxLength(200);
            builder.Property(e => e.FileName).IsRequired().HasMaxLength(160);
            builder.Property(e => e.ContentType).IsRequired().HasMaxLength(80);

            builder.HasIndex(e => e.IdVehicle);

            builder.HasOne<Vehicle>()
                .WithMany()
                .HasForeignKey(e => e.IdVehicle)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class VehicleStatusHistoryMap
        : EntityMap<VehicleStatusHistory>, IEntityTypeConfiguration<VehicleStatusHistory>
    {
        public override void Configure(EntityTypeBuilder<VehicleStatusHistory> builder)
        {
            builder.ToTable("VehicleStatusHistory");

            base.Configure(builder);

            builder.Property(e => e.Reason).HasMaxLength(240);

            builder.HasIndex(e => e.IdVehicle);

            builder.HasOne<Vehicle>()
                .WithMany()
                .HasForeignKey(e => e.IdVehicle)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
