using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace RevendaPro.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Clientes: quem ofereceu, quem comprou, quem volta (M21). A tabela Customer, e IdCustomer em
    /// Proposal e em Sale, opcionais para as linhas antigas, que o DbInitializer preenche na
    /// primeira subida. Ver docs/plans/m21-clientes.md. Os AlterColumn do gerador sairam, como
    /// nas migrations do M14.
    /// </summary>
    public partial class Customers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.AddColumn<int>(
                name: "IdCustomer",
                table: "Sale",
                type: "int",
                nullable: true)
                .Annotation("Relational:ColumnOrder", 5);


            migrationBuilder.AddColumn<int>(
                name: "IdCustomer",
                table: "Proposal",
                type: "int",
                nullable: true)
                .Annotation("Relational:ColumnOrder", 4);

            migrationBuilder.CreateTable(
                name: "Customer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<Guid>(type: "char(36)", nullable: false),
                    IdTenant = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    Document = table.Column<string>(type: "varchar(14)", maxLength: 14, nullable: true),
                    Phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: true),
                    Address = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: true),
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
                    table.PrimaryKey("PK_Customer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Customer_Tenant_IdTenant",
                        column: x => x.IdTenant,
                        principalTable: "Tenant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Sale_IdCustomer",
                table: "Sale",
                column: "IdCustomer");

            migrationBuilder.CreateIndex(
                name: "IX_Proposal_IdCustomer",
                table: "Proposal",
                column: "IdCustomer");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_Code",
                table: "Customer",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customer_IdTenant_Document",
                table: "Customer",
                columns: new[] { "IdTenant", "Document" });

            migrationBuilder.CreateIndex(
                name: "IX_Customer_IdTenant_Name",
                table: "Customer",
                columns: new[] { "IdTenant", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Customer_IdTenant_Phone",
                table: "Customer",
                columns: new[] { "IdTenant", "Phone" });

            migrationBuilder.AddForeignKey(
                name: "FK_Proposal_Customer_IdCustomer",
                table: "Proposal",
                column: "IdCustomer",
                principalTable: "Customer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sale_Customer_IdCustomer",
                table: "Sale",
                column: "IdCustomer",
                principalTable: "Customer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Proposal_Customer_IdCustomer",
                table: "Proposal");

            migrationBuilder.DropForeignKey(
                name: "FK_Sale_Customer_IdCustomer",
                table: "Sale");

            migrationBuilder.DropTable(
                name: "Customer");

            migrationBuilder.DropIndex(
                name: "IX_Sale_IdCustomer",
                table: "Sale");

            migrationBuilder.DropIndex(
                name: "IX_Proposal_IdCustomer",
                table: "Proposal");

            migrationBuilder.DropColumn(
                name: "IdCustomer",
                table: "Sale");

            migrationBuilder.DropColumn(
                name: "IdCustomer",
                table: "Proposal");

        }
    }
}
