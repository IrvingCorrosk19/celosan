using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SchoolManager.Models;

#nullable disable

namespace SchoolManager.Migrations
{
    [DbContext(typeof(SchoolDbContext))]
    [Migration("20260923180000_AddImportedGradeStatus")]
    public partial class AddImportedGradeStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_imported_trimester_grades_score_range",
                table: "student_imported_trimester_grades");

            migrationBuilder.AlterColumn<decimal>(
                name: "score",
                table: "student_imported_trimester_grades",
                type: "numeric(2,1)",
                precision: 2,
                scale: 1,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(2,1)",
                oldPrecision: 2,
                oldScale: 1);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "student_imported_trimester_grades",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Graded");

            migrationBuilder.AddCheckConstraint(
                name: "ck_imported_trimester_grades_status_score",
                table: "student_imported_trimester_grades",
                sql: "(status = 'Graded' AND score IS NOT NULL AND score >= 1.0 AND score <= 5.0) OR (status IN ('NoAsistio', 'SinNota') AND score IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_imported_trimester_grades_status_score",
                table: "student_imported_trimester_grades");

            migrationBuilder.DropColumn(
                name: "status",
                table: "student_imported_trimester_grades");

            migrationBuilder.Sql(@"
                ALTER TABLE student_imported_trimester_grades
                ALTER COLUMN score SET NOT NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_imported_trimester_grades_score_range",
                table: "student_imported_trimester_grades",
                sql: "score >= 1.0 AND score <= 5.0");
        }
    }
}
