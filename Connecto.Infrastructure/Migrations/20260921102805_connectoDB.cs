using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Connecto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class connectoDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    City = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Bio = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    VerificationStatus = table.Column<int>(type: "int", nullable: false),
                    ReputationScore = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Needs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BudgetMin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BudgetMax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RemoteAllowed = table.Column<bool>(type: "bit", nullable: false),
                    Deadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Needs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Needs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Offerings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PriceMin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PriceMax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExperienceYears = table.Column<int>(type: "int", nullable: false),
                    AvailableFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReputationScore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RemoteAvailable = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Offerings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Offerings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NeedSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NeedId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Skill = table.Column<int>(type: "int", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NeedSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NeedSkills_Needs_NeedId",
                        column: x => x.NeedId,
                        principalTable: "Needs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Match",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NeedId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SkillsScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    RequirementsScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    AvailabilityScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    BudgetScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LocationScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ExperienceScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ReputationScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Match", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Match_Needs_NeedId",
                        column: x => x.NeedId,
                        principalTable: "Needs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Match_Offerings_OfferingId",
                        column: x => x.OfferingId,
                        principalTable: "Offerings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OfferingSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Skill = table.Column<int>(type: "int", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferingSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfferingSkills_Offerings_OfferingId",
                        column: x => x.OfferingId,
                        principalTable: "Offerings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Address", "Bio", "City", "Country", "Email", "FirstName", "LastName", "MiddleName", "ReputationScore", "VerificationStatus" },
                values: new object[,]
                {
                    { new Guid("31d7a07e-a7bf-43b1-a3fe-17c178a738da"), "321 Elm St", "", "Wellington", "New Zealand", "amanda.lee@example.com", "Amanda", "Lee", "Arion", 0, 0 },
                    { new Guid("724f91cd-f077-4bb0-ac22-585d30eef49b"), "456 Oak Ave", "", "Wellington", "New Zealand", "bob.smith@example.com", "Bob", "Smith", "James", 0, 0 },
                    { new Guid("79c21000-178c-4ea7-bc4f-1013655fb774"), "123 Main St", "", "Auckland", "New Zealand", "alice.johnson@example.com", "Alice", "Johnson", "Marie", 0, 0 },
                    { new Guid("ed226b6e-7187-4d09-af10-9f9362af8399"), "789 Pine Rd", "", "Auckland", "New Zealand", "john.doe@example.com", "John", "Doe", "Dion", 0, 0 }
                });

            migrationBuilder.InsertData(
                table: "Needs",
                columns: new[] { "Id", "BudgetMax", "BudgetMin", "Category", "Deadline", "Description", "Location", "RemoteAllowed", "Status", "Title", "UserId" },
                values: new object[,]
                {
                    { new Guid("0b5ed194-8068-46d0-8dfb-d3c6499d2042"), null, null, "Creative", null, "Need a graphic designer", "Wellington", false, 0, "Need a graphic designer", new Guid("724f91cd-f077-4bb0-ac22-585d30eef49b") },
                    { new Guid("aa9e318d-748b-41b1-8907-dc215b2d0069"), null, null, "Technical", null, "Need a web developer", "Auckland", false, 0, "Need a web developer", new Guid("79c21000-178c-4ea7-bc4f-1013655fb774") }
                });

            migrationBuilder.InsertData(
                table: "Offerings",
                columns: new[] { "Id", "AvailableFrom", "Category", "Description", "ExperienceYears", "Location", "PriceMax", "PriceMin", "RemoteAvailable", "ReputationScore", "Status", "Title", "UserId" },
                values: new object[,]
                {
                    { new Guid("3e992743-8b83-4129-a40d-5504caf8a30f"), null, "Creative", "Offering graphic design services", 0, "Wellington", null, null, false, 0m, 0, "Offering graphic design services", new Guid("31d7a07e-a7bf-43b1-a3fe-17c178a738da") },
                    { new Guid("5ba88abb-f1c1-41a8-a140-666f388d71c7"), null, "Technical", "Offering web development services", 0, "Auckland", null, null, false, 0m, 0, "Offering web development services", new Guid("ed226b6e-7187-4d09-af10-9f9362af8399") }
                });

            migrationBuilder.InsertData(
                table: "NeedSkills",
                columns: new[] { "Id", "NeedId", "Skill" },
                values: new object[,]
                {
                    { new Guid("3c9d655c-2a27-4f1a-91c3-ae9d381ee884"), new Guid("0b5ed194-8068-46d0-8dfb-d3c6499d2042"), 0 },
                    { new Guid("996d971b-3b20-4bbd-8a22-c2ce01997931"), new Guid("aa9e318d-748b-41b1-8907-dc215b2d0069"), 1 }
                });

            migrationBuilder.InsertData(
                table: "OfferingSkills",
                columns: new[] { "Id", "OfferingId", "Skill" },
                values: new object[,]
                {
                    { new Guid("53ed7dd2-4bf4-4f93-b9aa-5056d2a7d031"), new Guid("3e992743-8b83-4129-a40d-5504caf8a30f"), 0 },
                    { new Guid("caa6bf2e-2238-4a5b-b3a9-e7060cb5c646"), new Guid("5ba88abb-f1c1-41a8-a140-666f388d71c7"), 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Match_NeedId",
                table: "Match",
                column: "NeedId");

            migrationBuilder.CreateIndex(
                name: "IX_Match_OfferingId",
                table: "Match",
                column: "OfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_Needs_UserId",
                table: "Needs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_NeedSkills_NeedId_Skill",
                table: "NeedSkills",
                columns: new[] { "NeedId", "Skill" });

            migrationBuilder.CreateIndex(
                name: "IX_Offerings_UserId",
                table: "Offerings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferingSkills_OfferingId_Skill",
                table: "OfferingSkills",
                columns: new[] { "OfferingId", "Skill" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Match");

            migrationBuilder.DropTable(
                name: "NeedSkills");

            migrationBuilder.DropTable(
                name: "OfferingSkills");

            migrationBuilder.DropTable(
                name: "Needs");

            migrationBuilder.DropTable(
                name: "Offerings");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
