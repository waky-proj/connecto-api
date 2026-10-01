using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Connecto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthSubjectId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthSubjectId",
                table: "Users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("25073136-a13e-4b78-881f-138062c9fb44"),
                column: "AuthSubjectId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("2ecd4582-6582-420a-8edc-96be5753de13"),
                column: "AuthSubjectId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("9d5377df-7f3b-4bce-84e4-0dc88984c35d"),
                column: "AuthSubjectId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("c4d3ee71-e814-407f-a7bc-be260734880f"),
                column: "AuthSubjectId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Users_AuthSubjectId",
                table: "Users",
                column: "AuthSubjectId",
                unique: true,
                filter: "[AuthSubjectId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_AuthSubjectId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AuthSubjectId",
                table: "Users");
        }
    }
}
