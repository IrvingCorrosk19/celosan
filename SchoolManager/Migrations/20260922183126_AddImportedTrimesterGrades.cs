using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class AddImportedTrimesterGrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_imported_trimester_grades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_subject_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trimester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(2,1)", precision: 2, scale: 1, nullable: false),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "ExcelImport"),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("student_imported_trimester_grades_pkey", x => x.id);
                    table.CheckConstraint("ck_imported_trimester_grades_score_range", "score >= 1.0 AND score <= 5.0");
                    table.ForeignKey(
                        name: "imported_trimester_grades_academic_year_id_fkey",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "imported_trimester_grades_assignment_id_fkey",
                        column: x => x.student_assignment_id,
                        principalTable: "student_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "imported_trimester_grades_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "imported_trimester_grades_import_batch_id_fkey",
                        column: x => x.import_batch_id,
                        principalTable: "celosan_bulk_import_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "imported_trimester_grades_school_id_fkey",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "imported_trimester_grades_ssa_id_fkey",
                        column: x => x.student_subject_assignment_id,
                        principalTable: "student_subject_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "imported_trimester_grades_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "imported_trimester_grades_trimester_id_fkey",
                        column: x => x.trimester_id,
                        principalTable: "trimester",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "imported_trimester_grades_updated_by_fkey",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_imported_trimester_grades_import_batch_id",
                table: "student_imported_trimester_grades",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_imported_trimester_grades_school_student_year",
                table: "student_imported_trimester_grades",
                columns: new[] { "school_id", "student_id", "academic_year_id" });

            migrationBuilder.CreateIndex(
                name: "uq_imported_trimester_grades_ssa_year_trimester",
                table: "student_imported_trimester_grades",
                columns: new[] { "school_id", "student_subject_assignment_id", "academic_year_id", "trimester_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_imported_trimester_grades");
        }
    }
}
