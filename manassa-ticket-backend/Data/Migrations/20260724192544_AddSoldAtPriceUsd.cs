using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace manassa_ticket_backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSoldAtPriceUsd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SoldAtPriceUsd",
                table: "Tickets",
                type: "numeric(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoldAtPriceUsd",
                table: "Tickets");
        }
    }
}
