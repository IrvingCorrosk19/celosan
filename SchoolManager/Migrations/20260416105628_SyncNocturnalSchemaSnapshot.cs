using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class SyncNocturnalSchemaSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "night_block_count",
                table: "school_schedule_configurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "night_block_duration_minutes",
                table: "school_schedule_configurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "night_start_time",
                table: "school_schedule_configurations",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "shift_id",
                table: "attendance",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "student_assignment_id",
                table: "attendance",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendance_shift_id",
                table: "attendance",
                column: "shift_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_student_assignment_id",
                table: "attendance",
                column: "student_assignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_student_date_group_grade_shift",
                table: "attendance",
                columns: new[] { "student_id", "date", "group_id", "grade_id", "shift_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "attendance_shift_id_fkey",
                table: "attendance",
                column: "shift_id",
                principalTable: "shifts",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "attendance_student_assignment_id_fkey",
                table: "attendance",
                column: "student_assignment_id",
                principalTable: "student_assignments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "attendance_shift_id_fkey",
                table: "attendance");

            migrationBuilder.DropForeignKey(
                name: "attendance_student_assignment_id_fkey",
                table: "attendance");

            migrationBuilder.DropIndex(
                name: "IX_attendance_shift_id",
                table: "attendance");

            migrationBuilder.DropIndex(
                name: "IX_attendance_student_assignment_id",
                table: "attendance");

            migrationBuilder.DropIndex(
                name: "ix_attendance_student_date_group_grade_shift",
                table: "attendance");

            migrationBuilder.DropColumn(
                name: "night_block_count",
                table: "school_schedule_configurations");

            migrationBuilder.DropColumn(
                name: "night_block_duration_minutes",
                table: "school_schedule_configurations");

            migrationBuilder.DropColumn(
                name: "night_start_time",
                table: "school_schedule_configurations");

            migrationBuilder.DropColumn(
                name: "shift_id",
                table: "attendance");

            migrationBuilder.DropColumn(
                name: "student_assignment_id",
                table: "attendance");
        }
    }
}
