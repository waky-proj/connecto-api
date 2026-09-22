using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Connecto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class connectoDB_22092026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "NeedSkills",
                keyColumn: "Id",
                keyValue: new Guid("3c9d655c-2a27-4f1a-91c3-ae9d381ee884"));

            migrationBuilder.DeleteData(
                table: "NeedSkills",
                keyColumn: "Id",
                keyValue: new Guid("996d971b-3b20-4bbd-8a22-c2ce01997931"));

            migrationBuilder.DeleteData(
                table: "OfferingSkills",
                keyColumn: "Id",
                keyValue: new Guid("53ed7dd2-4bf4-4f93-b9aa-5056d2a7d031"));

            migrationBuilder.DeleteData(
                table: "OfferingSkills",
                keyColumn: "Id",
                keyValue: new Guid("caa6bf2e-2238-4a5b-b3a9-e7060cb5c646"));

            migrationBuilder.DeleteData(
                table: "Needs",
                keyColumn: "Id",
                keyValue: new Guid("0b5ed194-8068-46d0-8dfb-d3c6499d2042"));

            migrationBuilder.DeleteData(
                table: "Needs",
                keyColumn: "Id",
                keyValue: new Guid("aa9e318d-748b-41b1-8907-dc215b2d0069"));

            migrationBuilder.DeleteData(
                table: "Offerings",
                keyColumn: "Id",
                keyValue: new Guid("3e992743-8b83-4129-a40d-5504caf8a30f"));

            migrationBuilder.DeleteData(
                table: "Offerings",
                keyColumn: "Id",
                keyValue: new Guid("5ba88abb-f1c1-41a8-a140-666f388d71c7"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("31d7a07e-a7bf-43b1-a3fe-17c178a738da"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("724f91cd-f077-4bb0-ac22-585d30eef49b"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("79c21000-178c-4ea7-bc4f-1013655fb774"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("ed226b6e-7187-4d09-af10-9f9362af8399"));

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableUntil",
                table: "Offerings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Address", "Bio", "City", "Country", "Email", "FirstName", "LastName", "MiddleName", "ReputationScore", "VerificationStatus" },
                values: new object[,]
                {
                    { new Guid("25073136-a13e-4b78-881f-138062c9fb44"), "789 Pine Rd", "", "Auckland", "New Zealand", "john.doe@example.com", "John", "Doe", "Dion", 0, 0 },
                    { new Guid("2ecd4582-6582-420a-8edc-96be5753de13"), "321 Elm St", "", "Wellington", "New Zealand", "amanda.lee@example.com", "Amanda", "Lee", "Arion", 0, 0 },
                    { new Guid("9d5377df-7f3b-4bce-84e4-0dc88984c35d"), "456 Oak Ave", "", "Wellington", "New Zealand", "bob.smith@example.com", "Bob", "Smith", "James", 0, 0 },
                    { new Guid("c4d3ee71-e814-407f-a7bc-be260734880f"), "123 Main St", "", "Auckland", "New Zealand", "alice.johnson@example.com", "Alice", "Johnson", "Marie", 0, 0 }
                });

            migrationBuilder.InsertData(
                table: "Needs",
                columns: new[] { "Id", "BudgetMax", "BudgetMin", "Category", "Deadline", "Description", "Location", "RemoteAllowed", "Status", "Title", "UserId" },
                values: new object[,]
                {
                    { new Guid("99bbb451-d3bb-4615-a371-688f1e82e0e8"), null, null, "Technical", null, "Need a web developer", "Auckland", false, 0, "Need a web developer", new Guid("c4d3ee71-e814-407f-a7bc-be260734880f") },
                    { new Guid("b5b8ec4d-f075-44d0-a431-c116504798c3"), null, null, "Creative", null, "Need a graphic designer", "Wellington", false, 0, "Need a graphic designer", new Guid("9d5377df-7f3b-4bce-84e4-0dc88984c35d") }
                });

            migrationBuilder.InsertData(
                table: "Offerings",
                columns: new[] { "Id", "AvailableFrom", "AvailableUntil", "Category", "Description", "ExperienceYears", "Location", "PriceMax", "PriceMin", "RemoteAvailable", "ReputationScore", "Status", "Title", "UserId" },
                values: new object[,]
                {
                    { new Guid("35e3b13f-4b46-44d1-a5aa-0b3e45c8741b"), null, null, "Technical", "Offering web development services", 0, "Auckland", null, null, false, 0m, 0, "Offering web development services", new Guid("25073136-a13e-4b78-881f-138062c9fb44") },
                    { new Guid("fae444c5-45d5-4375-befd-90eb566bfb23"), null, null, "Creative", "Offering graphic design services", 0, "Wellington", null, null, false, 0m, 0, "Offering graphic design services", new Guid("2ecd4582-6582-420a-8edc-96be5753de13") }
                });

            migrationBuilder.InsertData(
                table: "NeedSkills",
                columns: new[] { "Id", "NeedId", "Skill" },
                values: new object[,]
                {
                    { new Guid("60aac080-5a39-4ac0-b517-84f08c4375cf"), new Guid("b5b8ec4d-f075-44d0-a431-c116504798c3"), 1 },
                    { new Guid("63868ebc-8600-4c11-bdbc-025ee539dd5f"), new Guid("99bbb451-d3bb-4615-a371-688f1e82e0e8"), 2 }
                });

            migrationBuilder.InsertData(
                table: "OfferingSkills",
                columns: new[] { "Id", "OfferingId", "Skill" },
                values: new object[,]
                {
                    { new Guid("07518520-cf01-4ca4-bd28-ecd69d9abbee"), new Guid("35e3b13f-4b46-44d1-a5aa-0b3e45c8741b"), 2 },
                    { new Guid("374259b5-16e5-4bd5-b1cf-5680c6a117d2"), new Guid("fae444c5-45d5-4375-befd-90eb566bfb23"), 1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "NeedSkills",
                keyColumn: "Id",
                keyValue: new Guid("60aac080-5a39-4ac0-b517-84f08c4375cf"));

            migrationBuilder.DeleteData(
                table: "NeedSkills",
                keyColumn: "Id",
                keyValue: new Guid("63868ebc-8600-4c11-bdbc-025ee539dd5f"));

            migrationBuilder.DeleteData(
                table: "OfferingSkills",
                keyColumn: "Id",
                keyValue: new Guid("07518520-cf01-4ca4-bd28-ecd69d9abbee"));

            migrationBuilder.DeleteData(
                table: "OfferingSkills",
                keyColumn: "Id",
                keyValue: new Guid("374259b5-16e5-4bd5-b1cf-5680c6a117d2"));

            migrationBuilder.DeleteData(
                table: "Needs",
                keyColumn: "Id",
                keyValue: new Guid("99bbb451-d3bb-4615-a371-688f1e82e0e8"));

            migrationBuilder.DeleteData(
                table: "Needs",
                keyColumn: "Id",
                keyValue: new Guid("b5b8ec4d-f075-44d0-a431-c116504798c3"));

            migrationBuilder.DeleteData(
                table: "Offerings",
                keyColumn: "Id",
                keyValue: new Guid("35e3b13f-4b46-44d1-a5aa-0b3e45c8741b"));

            migrationBuilder.DeleteData(
                table: "Offerings",
                keyColumn: "Id",
                keyValue: new Guid("fae444c5-45d5-4375-befd-90eb566bfb23"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("25073136-a13e-4b78-881f-138062c9fb44"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("2ecd4582-6582-420a-8edc-96be5753de13"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("9d5377df-7f3b-4bce-84e4-0dc88984c35d"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("c4d3ee71-e814-407f-a7bc-be260734880f"));

            migrationBuilder.DropColumn(
                name: "AvailableUntil",
                table: "Offerings");

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
        }
    }
}
