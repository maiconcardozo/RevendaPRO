using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace RevendaPro.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class StoreExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "ExpenseType",
                type: "int",
                nullable: false,
                defaultValue: 1)
                .Annotation("Relational:ColumnOrder", 7);

            // O default da coluna preenche as linhas que ja existiam com o valor do TIPO, e nao
            // com o da coluna: sem este UPDATE elas ficam em zero, que e um tipo que jamais
            // aparece em lista nenhuma. Todo tipo anterior ao M22 e de carro, porque e o que ele e.
            migrationBuilder.Sql("UPDATE ExpenseType SET Scope = 1 WHERE Scope = 0;");

            migrationBuilder.CreateTable(
                name: "StoreExpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<Guid>(type: "char(36)", nullable: false),
                    IdTenant = table.Column<int>(type: "int", nullable: false),
                    IdExpenseType = table.Column<int>(type: "int", nullable: false),
                    IdSupplier = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsPaid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_StoreExpense", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreExpense_ExpenseType_IdExpenseType",
                        column: x => x.IdExpenseType,
                        principalTable: "ExpenseType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreExpense_Supplier_IdSupplier",
                        column: x => x.IdSupplier,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreExpense_Tenant_IdTenant",
                        column: x => x.IdTenant,
                        principalTable: "Tenant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StoreExpense_Code",
                table: "StoreExpense",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreExpense_IdExpenseType",
                table: "StoreExpense",
                column: "IdExpenseType");

            migrationBuilder.CreateIndex(
                name: "IX_StoreExpense_IdSupplier",
                table: "StoreExpense",
                column: "IdSupplier");

            migrationBuilder.CreateIndex(
                name: "IX_StoreExpense_IdTenant_Date",
                table: "StoreExpense",
                columns: new[] { "IdTenant", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreExpense_IsPaid_DueDate",
                table: "StoreExpense",
                columns: new[] { "IsPaid", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreExpense");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "ExpenseType");

        }
    }
}
