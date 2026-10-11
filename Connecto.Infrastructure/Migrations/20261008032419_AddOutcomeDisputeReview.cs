using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Connecto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutcomeDisputeReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisputeReviewNote",
                table: "Engagements",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisputeReviewedAt",
                table: "Engagements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisputeReviewerSubjectId",
                table: "Engagements",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisputeReviewNote",
                table: "Engagements");

            migrationBuilder.DropColumn(
                name: "DisputeReviewedAt",
                table: "Engagements");

            migrationBuilder.DropColumn(
                name: "DisputeReviewerSubjectId",
                table: "Engagements");
        }
    }
}
