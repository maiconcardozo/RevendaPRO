using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace RevendaPro.Infrastructure.Database.Migrations
{
    /// <summary>
    /// A passagem do carro pelos patios. Ver docs/plans/m14-patios.md.
    ///
    /// As duas chaves para Yard sao Restrict de proposito: um patio que sai do cadastro nao
    /// pode apagar a historia de quem passou por ele. O Cascade e so no veiculo, porque a
    /// passagem existe para contar a historia dele.
    /// </summary>
    public partial class YardHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehicleYardHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<Guid>(type: "char(36)", nullable: false),
                    IdVehicle = table.Column<int>(type: "int", nullable: false),
                    IdFromYard = table.Column<int>(type: "int", nullable: true),
                    IdToYard = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: true),
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
                    table.PrimaryKey("PK_VehicleYardHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleYardHistory_Vehicle_IdVehicle",
                        column: x => x.IdVehicle,
                        principalTable: "Vehicle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VehicleYardHistory_Yard_IdFromYard",
                        column: x => x.IdFromYard,
                        principalTable: "Yard",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleYardHistory_Yard_IdToYard",
                        column: x => x.IdToYard,
                        principalTable: "Yard",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleYardHistory_Code",
                table: "VehicleYardHistory",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleYardHistory_IdFromYard",
                table: "VehicleYardHistory",
                column: "IdFromYard");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleYardHistory_IdToYard",
                table: "VehicleYardHistory",
                column: "IdToYard");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleYardHistory_IdVehicle",
                table: "VehicleYardHistory",
                column: "IdVehicle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleYardHistory");
        }
    }
}
