using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Connecto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPilotSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PilotSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NeedId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CandidateCount = table.Column<int>(type: "int", nullable: true),
                    ClientKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PilotSignals_Needs_NeedId",
                        column: x => x.NeedId,
                        principalTable: "Needs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PilotSignals_Offerings_OfferingId",
                        column: x => x.OfferingId,
                        principalTable: "Offerings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PilotSignals_Users_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PilotSignals_Kind_OccurredAt",
                table: "PilotSignals",
                columns: new[] { "Kind", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PilotSignals_MemberId_Kind_ClientKey",
                table: "PilotSignals",
                columns: new[] { "MemberId", "Kind", "ClientKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PilotSignals_NeedId",
                table: "PilotSignals",
                column: "NeedId");

            migrationBuilder.CreateIndex(
                name: "IX_PilotSignals_OfferingId",
                table: "PilotSignals",
                column: "OfferingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PilotSignals");
        }
    }
}
