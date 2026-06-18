using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class AddCelosanCompletionFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "celosan_bulk_import_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    processed_rows = table.Column<int>(type: "integer", nullable: false),
                    success_rows = table.Column<int>(type: "integer", nullable: false),
                    error_rows = table.Column<int>(type: "integer", nullable: false),
                    error_summary = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("celosan_bulk_import_logs_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_celosan_bulk_import_logs_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_celosan_bulk_import_logs_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "student_identity_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Cedula"),
                    document_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    file_url = table.Column<string>(type: "text", nullable: false),
                    expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Valid"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("student_identity_documents_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_identity_documents_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_identity_documents_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_student_identity_documents_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_identity_documents_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_celosan_bulk_import_logs_created_at",
                table: "celosan_bulk_import_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_celosan_bulk_import_logs_created_by",
                table: "celosan_bulk_import_logs",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_celosan_bulk_import_logs_import_type",
                table: "celosan_bulk_import_logs",
                column: "import_type");

            migrationBuilder.CreateIndex(
                name: "ix_celosan_bulk_import_logs_school_id",
                table: "celosan_bulk_import_logs",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_identity_documents_created_by",
                table: "student_identity_documents",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_student_identity_documents_expiration_date",
                table: "student_identity_documents",
                column: "expiration_date");

            migrationBuilder.CreateIndex(
                name: "ix_student_identity_documents_school_id",
                table: "student_identity_documents",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_identity_documents_status",
                table: "student_identity_documents",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_student_identity_documents_student_id",
                table: "student_identity_documents",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_identity_documents_updated_by",
                table: "student_identity_documents",
                column: "updated_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "celosan_bulk_import_logs");

            migrationBuilder.DropTable(
                name: "student_identity_documents");
        }
    }
}
