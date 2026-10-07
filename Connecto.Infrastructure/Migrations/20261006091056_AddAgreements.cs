using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Connecto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Engagements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectionRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NeedId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NeedTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OfferingTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NeedRevision = table.Column<int>(type: "int", nullable: false),
                    OfferingRevision = table.Column<int>(type: "int", nullable: false),
                    Terms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PricingBasis = table.Column<int>(type: "int", nullable: false),
                    AgreedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcceptedRequestRevision = table.Column<int>(type: "int", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Engagements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Engagements_ConnectionRequests_ConnectionRequestId",
                        column: x => x.ConnectionRequestId,
                        principalTable: "ConnectionRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Engagements_Needs_NeedId",
                        column: x => x.NeedId,
                        principalTable: "Needs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Engagements_Offerings_OfferingId",
                        column: x => x.OfferingId,
                        principalTable: "Offerings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Engagements_Users_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Engagements_Users_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Engagements_ConnectionRequestId",
                table: "Engagements",
                column: "ConnectionRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Engagements_NeedId",
                table: "Engagements",
                column: "NeedId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Engagements_OfferingId",
                table: "Engagements",
                column: "OfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_Engagements_ProviderId",
                table: "Engagements",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Engagements_RequesterId",
                table: "Engagements",
                column: "RequesterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Engagements");
        }
    }
}
