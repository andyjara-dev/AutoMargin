using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remates.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BidResultPerVehicle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bids_auction_lots_auction_lot_id",
                table: "bids");

            migrationBuilder.AlterColumn<long>(
                name: "auction_lot_id",
                table: "bids",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "vehicle_id",
                table: "bids",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "ix_bids_vehicle_id",
                table: "bids",
                column: "vehicle_id");

            migrationBuilder.AddForeignKey(
                name: "fk_bids_auction_lots_auction_lot_id",
                table: "bids",
                column: "auction_lot_id",
                principalTable: "auction_lots",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_bids_vehicles_vehicle_id",
                table: "bids",
                column: "vehicle_id",
                principalTable: "vehicles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bids_auction_lots_auction_lot_id",
                table: "bids");

            migrationBuilder.DropForeignKey(
                name: "fk_bids_vehicles_vehicle_id",
                table: "bids");

            migrationBuilder.DropIndex(
                name: "ix_bids_vehicle_id",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "vehicle_id",
                table: "bids");

            migrationBuilder.AlterColumn<long>(
                name: "auction_lot_id",
                table: "bids",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_bids_auction_lots_auction_lot_id",
                table: "bids",
                column: "auction_lot_id",
                principalTable: "auction_lots",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
