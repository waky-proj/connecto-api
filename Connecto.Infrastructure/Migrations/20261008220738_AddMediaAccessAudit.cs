using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Connecto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaAccessAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaAccessAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MediaAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAccessAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaAccessAudits_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MediaAccessAudits_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAccessAudits_ActorId",
                table: "MediaAccessAudits",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAccessAudits_MediaAssetId_OccurredAt",
                table: "MediaAccessAudits",
                columns: new[] { "MediaAssetId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaAccessAudits");
        }
    }
}
