using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace jett_exchange_backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketDateSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketDateSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OriginalOwnerName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OriginalOwnerPassportNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TicketDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NumberOfBags = table.Column<int>(type: "integer", nullable: false),
                    TotalPriceUsd = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    TotalPriceJod = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    OriginalPrice = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    SellerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SellerEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    SellerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    PaymentInfo = table.Column<string>(type: "text", nullable: false),
                    Pin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SoldAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TicketFilePath = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BuyerName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BuyerEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketDateSubscriptions_Email_Date",
                table: "TicketDateSubscriptions",
                columns: new[] { "Email", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Pin",
                table: "Tickets",
                column: "Pin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TicketId",
                table: "Tickets",
                column: "TicketId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketDateSubscriptions");

            migrationBuilder.DropTable(
                name: "Tickets");
        }
    }
}
