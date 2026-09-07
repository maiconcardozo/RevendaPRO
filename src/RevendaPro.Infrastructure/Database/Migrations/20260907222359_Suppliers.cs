using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace RevendaPro.Infrastructure.Database.Migrations
{
    /// <summary>
    /// O cadastro de fornecedor, o de ramo, e a coluna que diz de quem foi cada gasto. Ver
    /// docs/plans/m18-fornecedores.md.
    ///
    /// Os 24 <c>AlterColumn</c> que o gerador emitiu no VehicleExpense sairam de proposito: eles
    /// so renumeravam a ordem fisica das colunas depois da coluna nova, e nada neste sistema le
    /// coluna por posicao. Mesmo motivo das migrations do M11 e do M14.
    /// </summary>
    public partial class Suppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdSupplier",
                table: "VehicleExpense",
                type: "int",
                nullable: true)
                .Annotation("Relational:ColumnOrder", 5);

            migrationBuilder.CreateTable(
                name: "SupplierSegment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<Guid>(type: "char(36)", nullable: false),
                    IdTenant = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DtCreated = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    DtUpdated = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    DtDeleted = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierSegment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierSegment_Tenant_IdTenant",
                        column: x => x.IdTenant,
                        principalTable: "Tenant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Supplier",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<Guid>(type: "char(36)", nullable: false),
                    IdTenant = table.Column<int>(type: "int", nullable: false),
                    IdSupplierSegment = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    ContactName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                    ContactPhone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Document = table.Column<string>(type: "varchar(14)", maxLength: 14, nullable: true),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DtCreated = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    DtUpdated = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    DtDeleted = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Supplier", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Supplier_SupplierSegment_IdSupplierSegment",
                        column: x => x.IdSupplierSegment,
                        principalTable: "SupplierSegment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Supplier_Tenant_IdTenant",
                        column: x => x.IdTenant,
                        principalTable: "Tenant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleExpense_IdSupplier",
                table: "VehicleExpense",
                column: "IdSupplier");

            migrationBuilder.CreateIndex(
                name: "IX_Supplier_Code",
                table: "Supplier",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Supplier_IdSupplierSegment",
                table: "Supplier",
                column: "IdSupplierSegment");

            migrationBuilder.CreateIndex(
                name: "IX_Supplier_IdTenant_Name",
                table: "Supplier",
                columns: new[] { "IdTenant", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSegment_Code",
                table: "SupplierSegment",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSegment_IdTenant_Position",
                table: "SupplierSegment",
                columns: new[] { "IdTenant", "Position" });

            migrationBuilder.AddForeignKey(
                name: "FK_VehicleExpense_Supplier_IdSupplier",
                table: "VehicleExpense",
                column: "IdSupplier",
                principalTable: "Supplier",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VehicleExpense_Supplier_IdSupplier",
                table: "VehicleExpense");

            migrationBuilder.DropTable(
                name: "Supplier");

            migrationBuilder.DropTable(
                name: "SupplierSegment");

            migrationBuilder.DropIndex(
                name: "IX_VehicleExpense_IdSupplier",
                table: "VehicleExpense");

            migrationBuilder.DropColumn(
                name: "IdSupplier",
                table: "VehicleExpense");
        }
    }
}
