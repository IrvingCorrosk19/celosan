using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentSubjectAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "student_subject_assignment_id",
                table: "student_activity_scores",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "student_subject_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    enrollment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Regular"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    school_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("student_subject_assignments_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_subject_assignments_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_student_subject_assignments_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_student_subject_assignments_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "student_subject_assignments_academic_year_id_fkey",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_subject_assignments_shift_id_fkey",
                        column: x => x.shift_id,
                        principalTable: "shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_subject_assignments_student_assignment_id_fkey",
                        column: x => x.student_assignment_id,
                        principalTable: "student_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_subject_assignments_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "student_subject_assignments_subject_assignment_id_fkey",
                        column: x => x.subject_assignment_id,
                        principalTable: "subject_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_scores_subject_enrollment_activity",
                table: "student_activity_scores",
                columns: new[] { "student_subject_assignment_id", "activity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_assignments_academic_year_id",
                table: "student_subject_assignments",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_subject_assignments_active_unique",
                table: "student_subject_assignments",
                columns: new[] { "student_id", "subject_assignment_id", "academic_year_id" },
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_assignments_created_by",
                table: "student_subject_assignments",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_assignments_school_id",
                table: "student_subject_assignments",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_assignments_shift_id",
                table: "student_subject_assignments",
                column: "shift_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_assignments_student_assignment_id",
                table: "student_subject_assignments",
                column: "student_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_assignments_student_id",
                table: "student_subject_assignments",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_assignments_subject_assignment_id",
                table: "student_subject_assignments",
                column: "subject_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_assignments_updated_by",
                table: "student_subject_assignments",
                column: "updated_by");

            migrationBuilder.Sql(
                """
                INSERT INTO student_subject_assignments
                (
                    id,
                    student_id,
                    subject_assignment_id,
                    student_assignment_id,
                    academic_year_id,
                    shift_id,
                    enrollment_type,
                    status,
                    is_active,
                    start_date,
                    school_id,
                    created_at
                )
                SELECT
                    gen_random_uuid(),
                    sub.student_id,
                    sub.subject_assignment_id,
                    sub.student_assignment_id,
                    sub.academic_year_id,
                    sub.shift_id,
                    sub.enrollment_type,
                    'Active',
                    sub.is_active,
                    sub.start_date,
                    sub.school_id,
                    sub.created_at
                FROM (
                    SELECT DISTINCT ON (sta.student_id, sa.id, sta.academic_year_id)
                        sta.student_id,
                        sa.id AS subject_assignment_id,
                        sta.id AS student_assignment_id,
                        sta.academic_year_id,
                        sta.shift_id,
                        COALESCE(sta.enrollment_type, 'Regular') AS enrollment_type,
                        sta.is_active,
                        COALESCE(sta.start_date, sta.created_at, NOW() AT TIME ZONE 'utc') AS start_date,
                        u.school_id,
                        COALESCE(sta.created_at, NOW() AT TIME ZONE 'utc') AS created_at
                    FROM student_assignments sta
                    INNER JOIN subject_assignments sa
                        ON sa.group_id = sta.group_id
                       AND sa.grade_level_id = sta.grade_id
                    INNER JOIN users u
                        ON u.id = sta.student_id
                    WHERE sta.is_active = true
                    ORDER BY sta.student_id, sa.id, sta.academic_year_id, sta.created_at DESC NULLS LAST, sta.id DESC
                ) sub
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM student_subject_assignments ssa
                    WHERE ssa.student_id = sub.student_id
                      AND ssa.subject_assignment_id = sub.subject_assignment_id
                      AND ssa.is_active = true
                      AND (
                          (ssa.academic_year_id IS NULL AND sub.academic_year_id IS NULL)
                          OR ssa.academic_year_id = sub.academic_year_id
                      )
                );
                """);

            migrationBuilder.Sql(
                """
                UPDATE student_activity_scores sas
                SET student_subject_assignment_id = sub.ssa_id
                FROM (
                    SELECT DISTINCT ON (score.id)
                        score.id AS score_id,
                        ssa.id AS ssa_id
                    FROM student_activity_scores score
                    INNER JOIN activities a ON a.id = score.activity_id
                    INNER JOIN student_subject_assignments ssa
                        ON ssa.student_id = score.student_id
                       AND ssa.is_active = true
                    INNER JOIN subject_assignments sa
                        ON sa.id = ssa.subject_assignment_id
                       AND sa.subject_id = a.subject_id
                       AND sa.group_id = a.group_id
                       AND sa.grade_level_id = a.grade_level_id
                    WHERE score.student_subject_assignment_id IS NULL
                    ORDER BY score.id, ssa.created_at DESC NULLS LAST
                ) sub
                WHERE sas.id = sub.score_id;
                """);

            migrationBuilder.AddForeignKey(
                name: "student_activity_scores_student_subject_assignment_id_fkey",
                table: "student_activity_scores",
                column: "student_subject_assignment_id",
                principalTable: "student_subject_assignments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "student_activity_scores_student_subject_assignment_id_fkey",
                table: "student_activity_scores");

            migrationBuilder.DropTable(
                name: "student_subject_assignments");

            migrationBuilder.DropIndex(
                name: "uq_scores_subject_enrollment_activity",
                table: "student_activity_scores");

            migrationBuilder.DropColumn(
                name: "student_subject_assignment_id",
                table: "student_activity_scores");
        }
    }
}
