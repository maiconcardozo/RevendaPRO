using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RevendaPro.Infrastructure.Database.Migrations
{
    /// <summary>
    /// O gasto ganha prazo e baixa (M22): quando vence, e quando o dinheiro saiu. Ver
    /// docs/plans/m22-caixa.md. Os AlterColumn do gerador sairam, como nas migrations do M14.
    ///
    /// O aproveitamento das linhas antigas vai aqui dentro, e nao numa rotina de subida: e uma
    /// correcao de uma vez so, e rodando na mesma transacao do schema ela jamais deixa uma
    /// coluna sem valor entre a criacao e o preenchimento. A coluna nasce aceitando nulo, recebe
    /// a data do gasto, e so entao passa a exigir valor.
    /// </summary>
    public partial class ExpenseDueAndPaidDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "VehicleExpense",
                type: "date",
                nullable: true)
                .Annotation("Relational:ColumnOrder", 11);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PaidDate",
                table: "VehicleExpense",
                type: "date",
                nullable: true)
                .Annotation("Relational:ColumnOrder", 12);

            // Todo gasto que ja existe vencia no dia em que foi lancado, e o que estava pago saiu
            // nesse mesmo dia: e o que o sistema sabia ate aqui, e e o unico chute honesto.
            migrationBuilder.Sql("UPDATE VehicleExpense SET DueDate = Date WHERE DueDate IS NULL;");
            migrationBuilder.Sql("UPDATE VehicleExpense SET PaidDate = Date WHERE IsPaid = 1 AND PaidDate IS NULL;");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DueDate",
                table: "VehicleExpense",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleExpense_IsPaid_DueDate",
                table: "VehicleExpense",
                columns: new[] { "IsPaid", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VehicleExpense_IsPaid_DueDate",
                table: "VehicleExpense");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "VehicleExpense");

            migrationBuilder.DropColumn(
                name: "PaidDate",
                table: "VehicleExpense");
        }
    }
}
