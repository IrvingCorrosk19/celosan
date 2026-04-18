using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class UqActiveStudentAssignmentEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Inscripciones por materia ligadas a matrículas duplicadas (misma clave activa)
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY student_id, grade_id, group_id, shift_id, academic_year_id
                               ORDER BY COALESCE(created_at, start_date, NOW()) DESC, id
                           ) AS rn
                    FROM student_assignments
                    WHERE is_active = true
                ),
                dup_sa AS (SELECT id FROM ranked WHERE rn > 1)
                UPDATE student_subject_assignments ssa
                SET is_active = false,
                    end_date = COALESCE(ssa.end_date, NOW()),
                    status = 'Inactive'
                WHERE ssa.is_active = true
                  AND ssa.student_assignment_id IS NOT NULL
                  AND ssa.student_assignment_id IN (SELECT id FROM dup_sa);
                """);

            // 2) Dejar una sola matrícula activa por (estudiante, grado, grupo, jornada, año)
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY student_id, grade_id, group_id, shift_id, academic_year_id
                               ORDER BY COALESCE(created_at, start_date, NOW()) DESC, id
                           ) AS rn
                    FROM student_assignments
                    WHERE is_active = true
                )
                UPDATE student_assignments sa
                SET is_active = false,
                    end_date = COALESCE(sa.end_date, NOW())
                FROM ranked r
                WHERE sa.id = r.id AND r.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "uq_student_assignments_active_enrollment",
                table: "student_assignments",
                columns: new[] { "student_id", "grade_id", "group_id", "shift_id", "academic_year_id" },
                unique: true,
                filter: "is_active = true")
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_student_assignments_active_enrollment",
                table: "student_assignments");
        }
    }
}
