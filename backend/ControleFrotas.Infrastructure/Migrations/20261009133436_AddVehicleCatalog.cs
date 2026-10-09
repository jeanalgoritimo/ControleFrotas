using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFrotas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BrandId",
                table: "Vehicles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModelId",
                table: "Vehicles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VehicleBrands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleBrands", x => x.Id);
                    table.UniqueConstraint("AK_VehicleBrands_CompanyId_Id", x => new { x.CompanyId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "VehicleModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BrandId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleModels", x => x.Id);
                    table.UniqueConstraint("AK_VehicleModels_CompanyId_BrandId_Id", x => new { x.CompanyId, x.BrandId, x.Id });
                    table.ForeignKey(
                        name: "FK_VehicleModels_VehicleBrands_CompanyId_BrandId",
                        columns: x => new { x.CompanyId, x.BrandId },
                        principalTable: "VehicleBrands",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            // Adopt existing vehicle labels before making the relationship mandatory.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Vehicles WHERE LEN(LTRIM(RTRIM(Brand))) = 0 OR LEN(LTRIM(RTRIM(Model))) = 0)
                    THROW 51001, 'Existem veículos sem marca ou modelo. Corrija os cadastros antes de atualizar.', 1;
                INSERT INTO VehicleBrands (CompanyId, Name, NormalizedName, Active)
                SELECT CompanyId, MIN(LTRIM(RTRIM(Brand))), UPPER(LTRIM(RTRIM(Brand))) COLLATE Latin1_General_100_BIN2, 1
                FROM Vehicles GROUP BY CompanyId, UPPER(LTRIM(RTRIM(Brand))) COLLATE Latin1_General_100_BIN2;
                INSERT INTO VehicleModels (CompanyId, BrandId, Name, NormalizedName, Active)
                SELECT v.CompanyId, b.Id, MIN(LTRIM(RTRIM(v.Model))), UPPER(LTRIM(RTRIM(v.Model))) COLLATE Latin1_General_100_BIN2, 1
                FROM Vehicles v JOIN VehicleBrands b ON b.CompanyId = v.CompanyId
                    AND b.NormalizedName = UPPER(LTRIM(RTRIM(v.Brand))) COLLATE Latin1_General_100_BIN2
                GROUP BY v.CompanyId, b.Id, UPPER(LTRIM(RTRIM(v.Model))) COLLATE Latin1_General_100_BIN2;
                UPDATE v SET BrandId = m.BrandId, ModelId = m.Id
                FROM Vehicles v JOIN VehicleBrands b ON b.CompanyId = v.CompanyId
                    AND b.NormalizedName = UPPER(LTRIM(RTRIM(v.Brand))) COLLATE Latin1_General_100_BIN2
                JOIN VehicleModels m ON m.CompanyId = v.CompanyId AND m.BrandId = b.Id
                    AND m.NormalizedName = UPPER(LTRIM(RTRIM(v.Model))) COLLATE Latin1_General_100_BIN2;
                """);
            migrationBuilder.AlterColumn<int>(name: "BrandId", table: "Vehicles", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ModelId", table: "Vehicles", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CompanyId_BrandId_ModelId",
                table: "Vehicles",
                columns: new[] { "CompanyId", "BrandId", "ModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleBrands_CompanyId_NormalizedName",
                table: "VehicleBrands",
                columns: new[] { "CompanyId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleModels_CompanyId_BrandId_NormalizedName",
                table: "VehicleModels",
                columns: new[] { "CompanyId", "BrandId", "NormalizedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_VehicleModels_CompanyId_BrandId_ModelId",
                table: "Vehicles",
                columns: new[] { "CompanyId", "BrandId", "ModelId" },
                principalTable: "VehicleModels",
                principalColumns: new[] { "CompanyId", "BrandId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_VehicleModels_CompanyId_BrandId_ModelId",
                table: "Vehicles");

            migrationBuilder.DropTable(
                name: "VehicleModels");

            migrationBuilder.DropTable(
                name: "VehicleBrands");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_CompanyId_BrandId_ModelId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "BrandId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ModelId",
                table: "Vehicles");
        }
    }
}
