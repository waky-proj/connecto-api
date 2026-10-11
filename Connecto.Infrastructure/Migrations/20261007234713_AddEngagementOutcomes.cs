using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Connecto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OutcomeRevision",
                table: "Engagements",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "OutcomeState",
                table: "Engagements",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "awaiting_confirmation");

            migrationBuilder.AddColumn<DateTime>(
                name: "OutcomeUpdatedAt",
                table: "Engagements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EngagementOutcomeEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ReportedOutcome = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngagementOutcomeEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EngagementOutcomeEvents_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EngagementOutcomeEvents_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EngagementOutcomeEvents_ActorId",
                table: "EngagementOutcomeEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementOutcomeEvents_EngagementId_ActorId_IdempotencyKey",
                table: "EngagementOutcomeEvents",
                columns: new[] { "EngagementId", "ActorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EngagementOutcomeEvents_EngagementId_Id",
                table: "EngagementOutcomeEvents",
                columns: new[] { "EngagementId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EngagementOutcomeEvents");

            migrationBuilder.DropColumn(
                name: "OutcomeRevision",
                table: "Engagements");

            migrationBuilder.DropColumn(
                name: "OutcomeState",
                table: "Engagements");

            migrationBuilder.DropColumn(
                name: "OutcomeUpdatedAt",
                table: "Engagements");
        }
    }
}
