using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Connecto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EngagementReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevieweeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerRole = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Reliability = table.Column<int>(type: "int", nullable: true),
                    Communication = table.Column<int>(type: "int", nullable: true),
                    Agreement = table.Column<int>(type: "int", nullable: true),
                    Clarity = table.Column<int>(type: "int", nullable: true),
                    Commitments = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublicationDelayDays = table.Column<int>(type: "int", nullable: false),
                    PublishAfterAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExcludedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExclusionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExcludedBySubjectId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngagementReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EngagementReviews_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EngagementReviews_Users_RevieweeId",
                        column: x => x.RevieweeId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EngagementReviews_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EngagementReviews_EngagementId_ReviewerId",
                table: "EngagementReviews",
                columns: new[] { "EngagementId", "ReviewerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EngagementReviews_RevieweeId",
                table: "EngagementReviews",
                column: "RevieweeId");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementReviews_ReviewerId",
                table: "EngagementReviews",
                column: "ReviewerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EngagementReviews");
        }
    }
}
