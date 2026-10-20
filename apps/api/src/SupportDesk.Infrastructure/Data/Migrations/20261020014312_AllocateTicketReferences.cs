using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllocateTicketReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateSequence(
                name: "TicketReferenceNumbers",
                schema: "dbo",
                startValue: 41L);

            // Existing databases can have references above the fixed seed range.
            // Allocation is allowed to have gaps; it must never reuse an existing reference.
            migrationBuilder.Sql("""
                DECLARE @next bigint = (
                    SELECT COALESCE(MAX(TRY_CONVERT(bigint, SUBSTRING([Reference], 5, 16))), 40) + 1
                    FROM [Tickets] WHERE [Reference] LIKE 'TCK-%');
                IF @next > 41
                BEGIN
                    DECLARE @sql nvarchar(200) = N'ALTER SEQUENCE [dbo].[TicketReferenceNumbers] RESTART WITH ' + CONVERT(nvarchar(20), @next);
                    EXEC(@sql);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "TicketReferenceNumbers",
                schema: "dbo");
        }
    }
}
