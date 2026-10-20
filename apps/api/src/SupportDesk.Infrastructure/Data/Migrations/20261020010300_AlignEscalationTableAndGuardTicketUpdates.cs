using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignEscalationTableAndGuardTicketUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketEscalation_Agents_FromAgentId",
                table: "TicketEscalation");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketEscalation_Agents_ToAgentId",
                table: "TicketEscalation");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketEscalation_Tickets_TicketId",
                table: "TicketEscalation");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TicketEscalation",
                table: "TicketEscalation");

            migrationBuilder.RenameTable(
                name: "TicketEscalation",
                newName: "TicketEscalations");

            migrationBuilder.RenameIndex(
                name: "IX_TicketEscalation_ToAgentId",
                table: "TicketEscalations",
                newName: "IX_TicketEscalations_ToAgentId");

            migrationBuilder.RenameIndex(
                name: "IX_TicketEscalation_TicketId_EscalatedAtUtc_Id",
                table: "TicketEscalations",
                newName: "IX_TicketEscalations_TicketId_EscalatedAtUtc_Id");

            migrationBuilder.RenameIndex(
                name: "IX_TicketEscalation_FromAgentId",
                table: "TicketEscalations",
                newName: "IX_TicketEscalations_FromAgentId");

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_TicketEscalations",
                table: "TicketEscalations",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketEscalations_Agents_FromAgentId",
                table: "TicketEscalations",
                column: "FromAgentId",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketEscalations_Agents_ToAgentId",
                table: "TicketEscalations",
                column: "ToAgentId",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketEscalations_Tickets_TicketId",
                table: "TicketEscalations",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketEscalations_Agents_FromAgentId",
                table: "TicketEscalations");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketEscalations_Agents_ToAgentId",
                table: "TicketEscalations");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketEscalations_Tickets_TicketId",
                table: "TicketEscalations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TicketEscalations",
                table: "TicketEscalations");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Tickets");

            migrationBuilder.RenameTable(
                name: "TicketEscalations",
                newName: "TicketEscalation");

            migrationBuilder.RenameIndex(
                name: "IX_TicketEscalations_ToAgentId",
                table: "TicketEscalation",
                newName: "IX_TicketEscalation_ToAgentId");

            migrationBuilder.RenameIndex(
                name: "IX_TicketEscalations_TicketId_EscalatedAtUtc_Id",
                table: "TicketEscalation",
                newName: "IX_TicketEscalation_TicketId_EscalatedAtUtc_Id");

            migrationBuilder.RenameIndex(
                name: "IX_TicketEscalations_FromAgentId",
                table: "TicketEscalation",
                newName: "IX_TicketEscalation_FromAgentId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TicketEscalation",
                table: "TicketEscalation",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketEscalation_Agents_FromAgentId",
                table: "TicketEscalation",
                column: "FromAgentId",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketEscalation_Agents_ToAgentId",
                table: "TicketEscalation",
                column: "ToAgentId",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketEscalation_Tickets_TicketId",
                table: "TicketEscalation",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
