using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace RevendaPro.Infrastructure.Database.Migrations
{
    /// <summary>
    /// A tabela de cotações da FIPE, e o ano-combustível no veículo. Ver ADR-0005.
    ///
    /// O gerador também emitiu doze <c>AlterColumn</c> no Vehicle, todos apenas para renumerar
    /// a ordem física das colunas depois da coluna nova. Foram retirados de propósito: nada
    /// neste sistema lê coluna por posição — o Dapper lê por nome e o SQL é escrito à mão —,
    /// e cada um deles seria um MODIFY COLUMN reconstruindo a tabela sem mudar nada. O
    /// snapshot do modelo guarda a ordem nova, então as próximas migrations saem limpas.
    /// </summary>
    public partial class FipeQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FipeYearFuel",
                table: "Vehicle",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FipeQuote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<Guid>(type: "char(36)", nullable: false),
                    FipeCode = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    YearFuel = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    ReferenceMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ModelYear = table.Column<short>(type: "smallint", nullable: false),
                    Brand = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    Model = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
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
                    table.PrimaryKey("PK_FipeQuote", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FipeQuote_Code",
                table: "FipeQuote",
                column: "Code",
                unique: true);

            // Uma cotação por modelo, ano-combustível e mês. Aqui o índice único é seguro, ao
            // contrário do que aconteceria com a placa: o sistema jamais exclui uma cotação.
            migrationBuilder.CreateIndex(
                name: "IX_FipeQuote_FipeCode_YearFuel_ReferenceMonth",
                table: "FipeQuote",
                columns: new[] { "FipeCode", "YearFuel", "ReferenceMonth" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FipeQuote");

            migrationBuilder.DropColumn(
                name: "FipeYearFuel",
                table: "Vehicle");
        }
    }
}
