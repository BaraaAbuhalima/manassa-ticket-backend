using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace manassa_ticket_backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class StorePaymentMethodAsString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A plain AlterColumn<string> would cast existing integer codes to their numeric
            // text ("0", "1", "2") instead of the enum names, corrupting every existing row.
            // The USING clause maps each stored code to its name explicitly instead.
            migrationBuilder.Sql("""
                ALTER TABLE "Tickets" ALTER COLUMN "PaymentMethod" TYPE character varying(20)
                USING (CASE "PaymentMethod"
                    WHEN 0 THEN 'Iban'
                    WHEN 1 THEN 'Reflect'
                    WHEN 2 THEN 'Phone'
                END);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Tickets" ALTER COLUMN "PaymentMethod" TYPE integer
                USING (CASE "PaymentMethod"
                    WHEN 'Iban' THEN 0
                    WHEN 'Reflect' THEN 1
                    WHEN 'Phone' THEN 2
                END);
                """);
        }
    }
}
