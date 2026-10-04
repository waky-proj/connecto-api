using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Connecto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListingTermsAndLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Offerings",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedHours",
                table: "Offerings",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PricingBasis",
                table: "Offerings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Offerings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE [Needs] SET [Status] = CASE [Status] WHEN 0 THEN 1 WHEN 1 THEN 1 WHEN 2 THEN 3 ELSE [Status] END;");
            migrationBuilder.Sql("UPDATE [Offerings] SET [Status] = CASE [Status] WHEN 0 THEN 1 WHEN 1 THEN 2 WHEN 2 THEN 3 ELSE [Status] END;");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Needs",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedHours",
                table: "Needs",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PricingBasis",
                table: "Needs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Needs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Needs",
                keyColumn: "Id",
                keyValue: new Guid("99bbb451-d3bb-4615-a371-688f1e82e0e8"),
                columns: new[] { "Currency", "EstimatedHours", "PricingBasis", "Revision", "Status" },
                values: new object[] { null, null, 0, 0, 1 });

            migrationBuilder.UpdateData(
                table: "Needs",
                keyColumn: "Id",
                keyValue: new Guid("b5b8ec4d-f075-44d0-a431-c116504798c3"),
                columns: new[] { "Currency", "EstimatedHours", "PricingBasis", "Revision", "Status" },
                values: new object[] { null, null, 0, 0, 1 });

            migrationBuilder.UpdateData(
                table: "Offerings",
                keyColumn: "Id",
                keyValue: new Guid("35e3b13f-4b46-44d1-a5aa-0b3e45c8741b"),
                columns: new[] { "Currency", "EstimatedHours", "PricingBasis", "Revision", "Status" },
                values: new object[] { null, null, 0, 0, 1 });

            migrationBuilder.UpdateData(
                table: "Offerings",
                keyColumn: "Id",
                keyValue: new Guid("fae444c5-45d5-4375-befd-90eb566bfb23"),
                columns: new[] { "Currency", "EstimatedHours", "PricingBasis", "Revision", "Status" },
                values: new object[] { null, null, 0, 0, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "EstimatedHours",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "PricingBasis",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Needs");

            migrationBuilder.DropColumn(
                name: "EstimatedHours",
                table: "Needs");

            migrationBuilder.DropColumn(
                name: "PricingBasis",
                table: "Needs");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Needs");

            migrationBuilder.UpdateData(
                table: "Needs",
                keyColumn: "Id",
                keyValue: new Guid("99bbb451-d3bb-4615-a371-688f1e82e0e8"),
                column: "Status",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Needs",
                keyColumn: "Id",
                keyValue: new Guid("b5b8ec4d-f075-44d0-a431-c116504798c3"),
                column: "Status",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Offerings",
                keyColumn: "Id",
                keyValue: new Guid("35e3b13f-4b46-44d1-a5aa-0b3e45c8741b"),
                column: "Status",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Offerings",
                keyColumn: "Id",
                keyValue: new Guid("fae444c5-45d5-4375-befd-90eb566bfb23"),
                column: "Status",
                value: 0);
        }
    }
}
