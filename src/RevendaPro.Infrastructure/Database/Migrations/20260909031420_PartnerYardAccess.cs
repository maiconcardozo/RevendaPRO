using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RevendaPro.Infrastructure.Database.Migrations
{
    /// <summary>
    /// O pátio a que uma pessoa está presa (M24).
    ///
    /// Nula em toda linha que já existe, e é o certo: quem usa o sistema hoje enxerga o pátio
    /// inteiro, e continua enxergando. Por isso esta migration não tem <c>UPDATE</c> nenhum —
    /// ao contrário do <c>Scope</c> do M22, onde o padrão da coluna deixava treze linhas com um
    /// valor inválido.
    ///
    /// A chave é <b>restrict</b>, como toda chave do pátio desde o M14: apagar um pátio jamais
    /// pode soltar o parceiro no estoque inteiro sem alguém decidir isso.
    ///
    /// O gerador escreveu junto uma dúzia de <c>AlterColumn</c> que só reordenam colunas — nada
    /// muda no banco, e cada uma é um <c>ALTER TABLE</c> a mais numa tabela grande. Foram
    /// removidas, como nos marcos anteriores.
    /// </summary>
    public partial class PartnerYardAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<int>(
                name: "IdYard",
                table: "User",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_IdYard",
                table: "User",
                column: "IdYard");

            migrationBuilder.AddForeignKey(
                name: "FK_User_Yard_IdYard",
                table: "User",
                column: "IdYard",
                principalTable: "Yard",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(name: "FK_User_Yard_IdYard", table: "User");

            migrationBuilder.DropIndex(name: "IX_User_IdYard", table: "User");

            migrationBuilder.DropColumn(name: "IdYard", table: "User");
        }
    }
}
