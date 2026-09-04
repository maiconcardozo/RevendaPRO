using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace RevendaPro.Infrastructure.Database.Migrations
{
    /// <summary>
    /// O cadastro de patio, e a coluna que diz onde cada carro esta. Ver
    /// docs/plans/m14-patios.md.
    ///
    /// Os 76 <c>AlterColumn</c> que o gerador emitiu no Vehicle sairam de proposito: eles so
    /// renumeravam a ordem fisica das colunas depois da coluna nova, e nada neste sistema le
    /// coluna por posicao. Mesmo motivo das migrations do M11.
    /// </summary>
    public partial class Yards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdYard",
                table: "Vehicle",
                type: "int",
                nullable: true)
                .Annotation("Relational:ColumnOrder", 5);

            migrationBuilder.CreateTable(
                name: "Yard",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<Guid>(type: "char(36)", nullable: false),
                    IdTenant = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ContactName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                    ContactPhone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    CutPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CutAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Yard", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Yard_Tenant_IdTenant",
                        column: x => x.IdTenant,
                        principalTable: "Tenant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_IdTenant_IdYard",
                table: "Vehicle",
                columns: new[] { "IdTenant", "IdYard" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_IdYard",
                table: "Vehicle",
                column: "IdYard");

            migrationBuilder.CreateIndex(
                name: "IX_Yard_Code",
                table: "Yard",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Yard_IdTenant_Position",
                table: "Yard",
                columns: new[] { "IdTenant", "Position" });

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicle_Yard_IdYard",
                table: "Vehicle",
                column: "IdYard",
                principalTable: "Yard",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Vehicle_Yard_IdYard", table: "Vehicle");
            migrationBuilder.DropTable(name: "Yard");
            migrationBuilder.DropIndex(name: "IX_Vehicle_IdTenant_IdYard", table: "Vehicle");
            migrationBuilder.DropIndex(name: "IX_Vehicle_IdYard", table: "Vehicle");
            migrationBuilder.DropColumn(name: "IdYard", table: "Vehicle");
        }
    }
}
