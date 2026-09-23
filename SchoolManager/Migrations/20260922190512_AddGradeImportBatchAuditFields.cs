using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeImportBatchAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "academic_year_id",
                table: "celosan_bulk_import_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "new_count",
                table: "celosan_bulk_import_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "unchanged_count",
                table: "celosan_bulk_import_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "update_count",
                table: "celosan_bulk_import_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_celosan_bulk_import_logs_academic_year_id",
                table: "celosan_bulk_import_logs",
                column: "academic_year_id");

            migrationBuilder.AddForeignKey(
                name: "FK_celosan_bulk_import_logs_academic_years_academic_year_id",
                table: "celosan_bulk_import_logs",
                column: "academic_year_id",
                principalTable: "academic_years",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_celosan_bulk_import_logs_academic_years_academic_year_id",
                table: "celosan_bulk_import_logs");

            migrationBuilder.DropIndex(
                name: "ix_celosan_bulk_import_logs_academic_year_id",
                table: "celosan_bulk_import_logs");

            migrationBuilder.DropColumn(
                name: "academic_year_id",
                table: "celosan_bulk_import_logs");

            migrationBuilder.DropColumn(
                name: "new_count",
                table: "celosan_bulk_import_logs");

            migrationBuilder.DropColumn(
                name: "unchanged_count",
                table: "celosan_bulk_import_logs");

            migrationBuilder.DropColumn(
                name: "update_count",
                table: "celosan_bulk_import_logs");
        }
    }
}
