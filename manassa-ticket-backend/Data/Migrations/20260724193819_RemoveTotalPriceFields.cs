using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace manassa_ticket_backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTotalPriceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rows created before SellerAskedPriceJod existed have it NULL; backfill from the
            // column being dropped so no asking price is lost before the NOT NULL constraint
            // and the DropColumn calls below run.
            migrationBuilder.Sql("""
                UPDATE "Tickets" SET "SellerAskedPriceJod" = "TotalPriceJod"
                WHERE "SellerAskedPriceJod" IS NULL;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "SellerAskedPriceJod",
                table: "Tickets",
                type: "numeric(10,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "TotalPriceJod",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TotalPriceUsd",
                table: "Tickets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalPriceJod",
                table: "Tickets",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPriceUsd",
                table: "Tickets",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            // Reconstruct both from the still-live SellerAskedPriceJod rather than leaving the
            // defaultValue-0 placeholders above in place.
            migrationBuilder.Sql("""
                UPDATE "Tickets" SET
                    "TotalPriceJod" = "SellerAskedPriceJod",
                    "TotalPriceUsd" = ROUND("SellerAskedPriceJod" * 1.43, 2);
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "SellerAskedPriceJod",
                table: "Tickets",
                type: "numeric(10,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)");
        }
    }
}
