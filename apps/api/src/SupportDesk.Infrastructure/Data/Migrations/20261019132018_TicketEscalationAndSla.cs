using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TicketEscalationAndSla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Tickets",
                type: "datetime2(7)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(3)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ResolvedAtUtc",
                table: "Tickets",
                type: "datetime2(7)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DueAtUtc",
                table: "Tickets",
                type: "datetime2(7)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Tickets",
                type: "datetime2(7)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(3)");

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaAtRiskAtUtc",
                table: "Tickets",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TicketEscalation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    FromPriority = table.Column<int>(type: "int", nullable: false),
                    ToPriority = table.Column<int>(type: "int", nullable: false),
                    FromAgentId = table.Column<int>(type: "int", nullable: true),
                    ToAgentId = table.Column<int>(type: "int", nullable: true),
                    FromDueAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ToDueAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EscalatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EscalatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketEscalation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketEscalation_Agents_FromAgentId",
                        column: x => x.FromAgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketEscalation_Agents_ToAgentId",
                        column: x => x.ToAgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketEscalation_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalation_FromAgentId",
                table: "TicketEscalation",
                column: "FromAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalation_TicketId_EscalatedAtUtc_Id",
                table: "TicketEscalation",
                columns: new[] { "TicketId", "EscalatedAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalation_ToAgentId",
                table: "TicketEscalation",
                column: "ToAgentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketEscalation");

            migrationBuilder.DropColumn(
                name: "SlaAtRiskAtUtc",
                table: "Tickets");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Tickets",
                type: "datetime2(3)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(7)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ResolvedAtUtc",
                table: "Tickets",
                type: "datetime2(3)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(7)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DueAtUtc",
                table: "Tickets",
                type: "datetime2(3)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(7)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Tickets",
                type: "datetime2(3)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(7)");
        }
    }
}
