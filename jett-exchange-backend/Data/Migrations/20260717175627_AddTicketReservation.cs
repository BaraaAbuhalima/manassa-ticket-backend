using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace jett_exchange_backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReservedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservedAt",
                table: "Tickets");
        }
    }
}
