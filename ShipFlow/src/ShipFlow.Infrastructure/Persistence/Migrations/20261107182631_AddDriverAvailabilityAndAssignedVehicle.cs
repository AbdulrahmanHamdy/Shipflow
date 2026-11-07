using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverAvailabilityAndAssignedVehicle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedVehicleId",
                table: "Drivers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "Drivers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_AssignedVehicle",
                table: "Drivers",
                column: "AssignedVehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Drivers_Vehicles_AssignedVehicleId",
                table: "Drivers",
                column: "AssignedVehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Drivers_Vehicles_AssignedVehicleId",
                table: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_AssignedVehicle",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "AssignedVehicleId",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "Drivers");
        }
    }
}
