using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Remates.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryCycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_movements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    movement_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vehicle_id = table.Column<long>(type: "bigint", nullable: true),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_cash_movements_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vehicle_id = table.Column<long>(type: "bigint", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    expense_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    supplier = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    document_ref = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    budgeted_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expenses", x => x.id);
                    table.ForeignKey(
                        name: "fk_expenses_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "listings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vehicle_id = table.Column<long>(type: "bigint", nullable: false),
                    channel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    list_price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    unpublished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    url = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listings", x => x.id);
                    table.ForeignKey(
                        name: "fk_listings_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchases",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vehicle_id = table.Column<long>(type: "bigint", nullable: false),
                    auction_lot_id = table.Column<long>(type: "bigint", nullable: true),
                    hammer_price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    commission_paid = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    purchase_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    invoice_ref = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    deal_analysis_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchases", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchases_auction_lots_auction_lot_id",
                        column: x => x.auction_lot_id,
                        principalTable: "auction_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_purchases_deal_analysis_deal_analysis_id",
                        column: x => x.deal_analysis_id,
                        principalTable: "deal_analysis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_purchases_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vehicle_id = table.Column<long>(type: "bigint", nullable: false),
                    sale_price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    sale_costs = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    sale_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    buyer_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    payment_method = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    days_in_inventory = table.Column<int>(type: "integer", nullable: false),
                    total_cash_invested = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    capital_cost = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    real_profit_cash = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    real_profit_economic = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    real_roi_cash = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    real_roi_economic = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    real_roi_annualized = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    real_margin_pct = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "price_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    listing_id = table.Column<long>(type: "bigint", nullable: false),
                    old_price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    new_price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_price_changes_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prediction_outcomes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vehicle_id = table.Column<long>(type: "bigint", nullable: false),
                    deal_analysis_id = table.Column<long>(type: "bigint", nullable: false),
                    sale_id = table.Column<long>(type: "bigint", nullable: false),
                    predicted_sale_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    actual_sale_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    predicted_repair_cost = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    actual_repair_cost = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    predicted_days = table.Column<int>(type: "integer", nullable: false),
                    actual_days = table.Column<int>(type: "integer", nullable: false),
                    predicted_profit = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    actual_profit = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    sale_value_error_pct = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    repair_cost_error_pct = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    days_error_pct = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    profit_error_pct = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    under_performed = table.Column<bool>(type: "boolean", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prediction_outcomes", x => x.id);
                    table.ForeignKey(
                        name: "fk_prediction_outcomes_deal_analysis_deal_analysis_id",
                        column: x => x.deal_analysis_id,
                        principalTable: "deal_analysis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prediction_outcomes_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_prediction_outcomes_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cash_movements_movement_date",
                table: "cash_movements",
                column: "movement_date");

            migrationBuilder.CreateIndex(
                name: "ix_cash_movements_type",
                table: "cash_movements",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "ix_cash_movements_vehicle_id",
                table: "cash_movements",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_vehicle_id_category",
                table: "expenses",
                columns: new[] { "vehicle_id", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_listings_vehicle_id",
                table: "listings",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_outcomes_closed_at",
                table: "prediction_outcomes",
                column: "closed_at");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_outcomes_deal_analysis_id",
                table: "prediction_outcomes",
                column: "deal_analysis_id");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_outcomes_sale_id",
                table: "prediction_outcomes",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_outcomes_vehicle_id",
                table: "prediction_outcomes",
                column: "vehicle_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_price_changes_listing_id_changed_at",
                table: "price_changes",
                columns: new[] { "listing_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_purchases_auction_lot_id",
                table: "purchases",
                column: "auction_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchases_deal_analysis_id",
                table: "purchases",
                column: "deal_analysis_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchases_vehicle_id",
                table: "purchases",
                column: "vehicle_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_sale_date",
                table: "sales",
                column: "sale_date");

            migrationBuilder.CreateIndex(
                name: "ix_sales_vehicle_id",
                table: "sales",
                column: "vehicle_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_movements");

            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "prediction_outcomes");

            migrationBuilder.DropTable(
                name: "price_changes");

            migrationBuilder.DropTable(
                name: "purchases");

            migrationBuilder.DropTable(
                name: "sales");

            migrationBuilder.DropTable(
                name: "listings");
        }
    }
}
