using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace manassa_ticket_backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class StoreTicketStatusAsString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A plain AlterColumn<string> would cast existing integer codes to their numeric
            // text ("0", "1", ...) instead of the enum names, corrupting every existing row.
            // The USING clause maps each stored code to its name explicitly instead.
            migrationBuilder.Sql("""
                ALTER TABLE "Tickets" ALTER COLUMN "Status" TYPE character varying(20)
                USING (CASE "Status"
                    WHEN 0 THEN 'Deleted'
                    WHEN 1 THEN 'ForSale'
                    WHEN 2 THEN 'Sold'
                    WHEN 3 THEN 'Processing'
                    WHEN 4 THEN 'Reserved'
                END);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Tickets" ALTER COLUMN "Status" TYPE integer
                USING (CASE "Status"
                    WHEN 'Deleted' THEN 0
                    WHEN 'ForSale' THEN 1
                    WHEN 'Sold' THEN 2
                    WHEN 'Processing' THEN 3
                    WHEN 'Reserved' THEN 4
                END);
                """);
        }
    }
}
