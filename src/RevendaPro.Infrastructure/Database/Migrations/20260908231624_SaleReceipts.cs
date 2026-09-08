using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace RevendaPro.Infrastructure.Database.Migrations
{
    /// <summary>
    /// O que entra por uma venda (M22): cada entrada de dinheiro, com data, e o prazo do que
    /// ainda falta. A venda registrada era dinheiro no bolso para o sistema, mesmo quando o
    /// banco paga em quinze dias. Ver docs/plans/m22-caixa.md. Os AlterColumn do gerador
    /// sairam, como nas migrations do M14.
    ///
    /// Sem aproveitamento de propósito: uma venda antiga foi paga de um jeito que o sistema
    /// jamais registrou, e inventar uma entrada para cada uma seria escrever no passado. Elas
    /// entram no caixa como o que sao - vendas sem entrada registrada - e quem quiser conciliar
    /// lanca a entrada a mao.
    /// </summary>
    public partial class SaleReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "Sale",
                type: "date",
                nullable: true)
                .Annotation("Relational:ColumnOrder", 21);

            migrationBuilder.CreateTable(
                name: "SaleReceipt",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<Guid>(type: "char(36)", nullable: false),
                    IdSale = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_SaleReceipt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleReceipt_Sale_IdSale",
                        column: x => x.IdSale,
                        principalTable: "Sale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReceipt_Code",
                table: "SaleReceipt",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleReceipt_Date",
                table: "SaleReceipt",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReceipt_IdSale",
                table: "SaleReceipt",
                column: "IdSale");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleReceipt");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Sale");

        }
    }
}
