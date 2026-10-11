using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Connecto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MediaAssetId",
                table: "SafetyReports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploaderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    QuarantineKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    DisplayKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemovalReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RemovedBySubjectId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaAssets_EngagementReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "EngagementReviews",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MediaAssets_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MediaAssets_Users_UploaderId",
                        column: x => x.UploaderId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyReports_MediaAssetId",
                table: "SafetyReports",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_EngagementId_UploaderId_Purpose",
                table: "MediaAssets",
                columns: new[] { "EngagementId", "UploaderId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_ReviewId",
                table: "MediaAssets",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_Stage_CreatedAt",
                table: "MediaAssets",
                columns: new[] { "Stage", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_UploaderId",
                table: "MediaAssets",
                column: "UploaderId");

            migrationBuilder.AddForeignKey(
                name: "FK_SafetyReports_MediaAssets_MediaAssetId",
                table: "SafetyReports",
                column: "MediaAssetId",
                principalTable: "MediaAssets",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SafetyReports_MediaAssets_MediaAssetId",
                table: "SafetyReports");

            migrationBuilder.DropTable(
                name: "MediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_SafetyReports_MediaAssetId",
                table: "SafetyReports");

            migrationBuilder.DropColumn(
                name: "MediaAssetId",
                table: "SafetyReports");
        }
    }
}
