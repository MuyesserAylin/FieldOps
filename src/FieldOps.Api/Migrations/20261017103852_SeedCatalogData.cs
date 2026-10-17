using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FieldOps.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedCatalogData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "skills",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 1, "Gas" },
                    { 2, "Plumbing" },
                    { 3, "Electrical" },
                    { 4, "RoadWork" }
                });

            migrationBuilder.InsertData(
                table: "fault_types",
                columns: new[] { "id", "default_urgency", "estimated_duration_minutes", "name", "required_skill_id" },
                values: new object[,]
                {
                    { 1, 4, 90, "Gas leak", 1 },
                    { 2, 3, 120, "Burst water pipe", 2 },
                    { 3, 3, 90, "Power outage", 3 },
                    { 4, 2, 60, "Pothole", 4 },
                    { 5, 1, 30, "Street light failure", 3 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "fault_types",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "fault_types",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "fault_types",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "fault_types",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "fault_types",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "skills",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "skills",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "skills",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "skills",
                keyColumn: "id",
                keyValue: 4);
        }
    }
}
