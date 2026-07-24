using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace manassa_ticket_backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerAskedPriceJod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SellerAskedPriceJod",
                table: "Tickets",
                type: "numeric(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SellerAskedPriceJod",
                table: "Tickets");
        }
    }
}
