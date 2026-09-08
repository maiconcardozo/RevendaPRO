using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RevendaPro.Infrastructure.Database.Migrations
{
    /// <summary>
    /// O que os documentos gerados dizem sobre a revenda: CNPJ, telefone, e-mail e endereco. Ver
    /// docs/plans/m19-relatorios.md. Os AlterColumn do gerador sairam, como nas migrations do M14.
    /// </summary>
    public partial class CompanyDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Tenant",
                type: "varchar(240)",
                maxLength: 240,
                nullable: true)
                .Annotation("Relational:ColumnOrder", 7);

            migrationBuilder.AddColumn<string>(
                name: "Document",
                table: "Tenant",
                type: "varchar(14)",
                maxLength: 14,
                nullable: true)
                .Annotation("Relational:ColumnOrder", 4);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Tenant",
                type: "varchar(160)",
                maxLength: 160,
                nullable: true)
                .Annotation("Relational:ColumnOrder", 6);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Tenant",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("Relational:ColumnOrder", 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Tenant");

            migrationBuilder.DropColumn(
                name: "Document",
                table: "Tenant");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Tenant");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Tenant");
        }
    }
}
