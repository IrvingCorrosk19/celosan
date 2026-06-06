using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectPromotionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subject_promotion_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trimester = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    final_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    student_subject_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    promoted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("subject_promotion_records_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_subject_promotion_records_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "subject_promotion_records_academic_year_id_fkey",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "subject_promotion_records_grade_level_id_fkey",
                        column: x => x.grade_level_id,
                        principalTable: "grade_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "subject_promotion_records_ssa_id_fkey",
                        column: x => x.student_subject_assignment_id,
                        principalTable: "student_subject_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "subject_promotion_records_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "subject_promotion_records_subject_id_fkey",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subject_promotion_records_academic_year_id",
                table: "subject_promotion_records",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "IX_subject_promotion_records_grade_level_id",
                table: "subject_promotion_records",
                column: "grade_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_subject_promotion_records_school_id",
                table: "subject_promotion_records",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_subject_promotion_records_student_id",
                table: "subject_promotion_records",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_subject_promotion_records_student_subject_assignment_id",
                table: "subject_promotion_records",
                column: "student_subject_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_subject_promotion_records_student_subject_year_trimester",
                table: "subject_promotion_records",
                columns: new[] { "student_id", "subject_id", "academic_year_id", "trimester" });

            migrationBuilder.CreateIndex(
                name: "IX_subject_promotion_records_subject_id",
                table: "subject_promotion_records",
                column: "subject_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subject_promotion_records");
        }
    }
}
