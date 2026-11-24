using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FieldOps.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentDecisionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assignment_decisions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_id = table.Column<long>(type: "bigint", nullable: false),
                    urgency = table.Column<int>(type: "integer", nullable: false),
                    weight_set = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_decisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_assignment_decisions_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignment_decision_candidates",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    decision_id = table.Column<long>(type: "bigint", nullable: false),
                    team_id = table.Column<int>(type: "integer", nullable: false),
                    distance_km = table.Column<double>(type: "double precision", nullable: false),
                    elimination_reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    distance_score = table.Column<double>(type: "double precision", nullable: true),
                    workload_score = table.Column<double>(type: "double precision", nullable: true),
                    skill_score = table.Column<double>(type: "double precision", nullable: true),
                    response_score = table.Column<double>(type: "double precision", nullable: true),
                    total_score = table.Column<double>(type: "double precision", nullable: true),
                    rank = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_decision_candidates", x => x.id);
                    table.CheckConstraint("ck_assignment_decision_candidates_rank", "rank IS NULL OR rank >= 1");
                    table.CheckConstraint("ck_assignment_decision_candidates_state", "(elimination_reason IS NOT NULL AND distance_score IS NULL AND workload_score IS NULL AND skill_score IS NULL AND response_score IS NULL AND total_score IS NULL AND rank IS NULL) OR (elimination_reason IS NULL AND distance_score IS NOT NULL AND workload_score IS NOT NULL AND skill_score IS NOT NULL AND response_score IS NOT NULL AND total_score IS NOT NULL AND rank IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_assignment_decision_candidates_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decision_candidates_decision",
                        column: x => x.decision_id,
                        principalTable: "assignment_decisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assignment_decision_candidates_decision_id_rank",
                table: "assignment_decision_candidates",
                columns: new[] { "decision_id", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assignment_decision_candidates_decision_id_team_id",
                table: "assignment_decision_candidates",
                columns: new[] { "decision_id", "team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assignment_decision_candidates_team_id",
                table: "assignment_decision_candidates",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_decisions_job_id_decided_at",
                table: "assignment_decisions",
                columns: new[] { "job_id", "decided_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignment_decision_candidates");

            migrationBuilder.DropTable(
                name: "assignment_decisions");
        }
    }
}
