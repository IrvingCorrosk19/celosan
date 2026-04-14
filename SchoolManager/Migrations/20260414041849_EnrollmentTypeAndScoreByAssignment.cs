using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class EnrollmentTypeAndScoreByAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_scores",
                table: "student_activity_scores");

            migrationBuilder.AddColumn<string>(
                name: "enrollment_type",
                table: "student_assignments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Regular");

            migrationBuilder.AddColumn<DateTime>(
                name: "start_date",
                table: "student_assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE student_assignments
                SET start_date = COALESCE(created_at, NOW() AT TIME ZONE 'utc')
                WHERE start_date IS NULL;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "student_assignment_id",
                table: "student_activity_scores",
                type: "uuid",
                nullable: true);

            // Notas → matrícula activa cuyo grupo coincide con la actividad (subconsulta: PG no permite alias del UPDATE en el JOIN del FROM)
            migrationBuilder.Sql(
                """
                UPDATE student_activity_scores s
                SET student_assignment_id = sub.sa_id
                FROM (
                    SELECT s2.id AS sid, sa.id AS sa_id
                    FROM student_activity_scores s2
                    INNER JOIN activities a ON a.id = s2.activity_id
                    INNER JOIN student_assignments sa ON sa.student_id = s2.student_id
                        AND sa.group_id = a.group_id
                        AND sa.is_active = true
                    WHERE a.group_id IS NOT NULL
                      AND s2.student_assignment_id IS NULL
                ) sub
                WHERE s.id = sub.sid;
                """);

            // Actividades sin grupo: emparejar por grado (una matrícula activa por estudiante-grado si hay varias, la más reciente)
            migrationBuilder.Sql(
                """
                UPDATE student_activity_scores s
                SET student_assignment_id = sub.sa_id
                FROM (
                    SELECT DISTINCT ON (s.id) s.id AS sid, sa.id AS sa_id
                    FROM student_activity_scores s
                    INNER JOIN activities a ON a.id = s.activity_id
                    INNER JOIN student_assignments sa ON sa.student_id = s.student_id
                        AND sa.is_active = true
                        AND sa.grade_id = a.grade_level_id
                    WHERE s.student_assignment_id IS NULL
                      AND a.group_id IS NULL
                      AND a.grade_level_id IS NOT NULL
                    ORDER BY s.id, sa.created_at DESC NULLS LAST
                ) sub
                WHERE s.id = sub.sid;
                """);

            // Último recurso: cualquier matrícula activa del estudiante
            migrationBuilder.Sql(
                """
                UPDATE student_activity_scores s
                SET student_assignment_id = (
                    SELECT sa.id
                    FROM student_assignments sa
                    WHERE sa.student_id = s.student_id AND sa.is_active = true
                    ORDER BY sa.created_at DESC NULLS LAST
                    LIMIT 1
                )
                WHERE s.student_assignment_id IS NULL;
                """);

            // BD de prueba: eliminar notas huérfanas (sin matrícula activa) en lugar de fallar la migración
            migrationBuilder.Sql(
                """
                DELETE FROM student_activity_scores WHERE student_assignment_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "student_assignment_id",
                table: "student_activity_scores",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_scores_assignment_activity",
                table: "student_activity_scores",
                columns: new[] { "student_assignment_id", "activity_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "student_activity_scores_student_assignment_id_fkey",
                table: "student_activity_scores",
                column: "student_assignment_id",
                principalTable: "student_assignments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "student_activity_scores_student_assignment_id_fkey",
                table: "student_activity_scores");

            migrationBuilder.DropIndex(
                name: "uq_scores_assignment_activity",
                table: "student_activity_scores");

            migrationBuilder.DropColumn(
                name: "student_assignment_id",
                table: "student_activity_scores");

            migrationBuilder.DropColumn(
                name: "enrollment_type",
                table: "student_assignments");

            migrationBuilder.DropColumn(
                name: "start_date",
                table: "student_assignments");

            migrationBuilder.CreateIndex(
                name: "uq_scores",
                table: "student_activity_scores",
                columns: new[] { "student_id", "activity_id" },
                unique: true);
        }
    }
}
