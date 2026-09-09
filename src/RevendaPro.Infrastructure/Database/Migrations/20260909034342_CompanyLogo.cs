using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RevendaPro.Infrastructure.Database.Migrations
{
    /// <summary>
    /// O ponteiro para o logotipo da revenda (M25).
    ///
    /// Só o nome do arquivo: o logotipo vive no bucket, como todo arquivo desde o M9. Nula em
    /// toda revenda que já existe, e é o certo — quem não subiu logotipo continua tirando os
    /// papéis como antes, e por isso a migration não tem <c>UPDATE</c> nenhum.
    ///
    /// O gerador escreveu junto uma dúzia de <c>AlterColumn</c> que só reordenam colunas.
    /// Removidas, como nos marcos anteriores.
    /// </summary>
    public partial class CompanyLogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<string>(
                name: "Logo",
                table: "Tenant",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(name: "Logo", table: "Tenant");
        }
    }
}
