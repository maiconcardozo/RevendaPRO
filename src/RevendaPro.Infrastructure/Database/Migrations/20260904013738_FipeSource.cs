using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RevendaPro.Infrastructure.Database.Migrations
{
    /// <summary>
    /// De onde veio o valor de referência do veículo: digitado ou lido da tabela.
    ///
    /// Nulo em todo carro já cadastrado, e é a leitura honesta: até aqui o sistema realmente
    /// ignorava a origem. A primeira edição ou consulta preenche.
    ///
    /// Os <c>AlterColumn</c> de renumeração de ordem física saíram de propósito, pelo mesmo
    /// motivo da migration anterior.
    /// </summary>
    public partial class FipeSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FipeSource",
                table: "Vehicle",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FipeSource",
                table: "Vehicle");
        }
    }
}
