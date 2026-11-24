using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldOps.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddJobAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "assigned_at",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "assigned_team_id",
                table: "jobs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_jobs_assigned_team_id_assigned_at",
                table: "jobs",
                columns: new[] { "assigned_team_id", "assigned_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_jobs_assignment_pair",
                table: "jobs",
                sql: "(assigned_team_id IS NULL) = (assigned_at IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_jobs_teams_assigned_team_id",
                table: "jobs",
                column: "assigned_team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_jobs_teams_assigned_team_id",
                table: "jobs");

            migrationBuilder.DropIndex(
                name: "ix_jobs_assigned_team_id_assigned_at",
                table: "jobs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_jobs_assignment_pair",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "assigned_at",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "assigned_team_id",
                table: "jobs");
        }
    }
}
