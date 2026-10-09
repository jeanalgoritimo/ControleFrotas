using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFrotas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFuelEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FuelEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Odometer = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    FuelType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Liters = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Station = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FullTank = table.Column<bool>(type: "bit", nullable: false),
                    KmPerLiter = table.Column<decimal>(type: "decimal(16,2)", precision: 16, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuelEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FuelEntries_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FuelEntries_CompanyId_RequestId",
                table: "FuelEntries",
                columns: new[] { "CompanyId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FuelEntries_CompanyId_VehicleId_Date_Id",
                table: "FuelEntries",
                columns: new[] { "CompanyId", "VehicleId", "Date", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FuelEntries_VehicleId",
                table: "FuelEntries",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FuelEntries");
        }
    }
}
