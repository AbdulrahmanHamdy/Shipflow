using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleSpecifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CargoHeightM",
                table: "Vehicles",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CargoLengthM",
                table: "Vehicles",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CargoWidthM",
                table: "Vehicles",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DoorHeightM",
                table: "Vehicles",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DoorWidthM",
                table: "Vehicles",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "Vehicles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CargoHeightM",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "CargoLengthM",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "CargoWidthM",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "DoorHeightM",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "DoorWidthM",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "Vehicles");
        }
    }
}
