using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class MedicalRecordRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastEditedByDoctorId",
                table: "MedicalRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MedicalRecordRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MedicalRecordId = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Diagnosis = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Treatment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ChangedFields = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EditedByDoctorId = table.Column<int>(type: "integer", nullable: false),
                    EditedByUserName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalRecordRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalRecordRevisions_Doctors_EditedByDoctorId",
                        column: x => x.EditedByDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicalRecordRevisions_MedicalRecords_MedicalRecordId",
                        column: x => x.MedicalRecordId,
                        principalTable: "MedicalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecords_LastEditedByDoctorId",
                table: "MedicalRecords",
                column: "LastEditedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecordRevisions_EditedByDoctorId",
                table: "MedicalRecordRevisions",
                column: "EditedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecordRevisions_MedicalRecordId_Revision",
                table: "MedicalRecordRevisions",
                columns: new[] { "MedicalRecordId", "Revision" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalRecords_Doctors_LastEditedByDoctorId",
                table: "MedicalRecords",
                column: "LastEditedByDoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Existing records get revision 1: their current content, by their author, at creation time.
            // Earlier edits were never stored, so they cannot be recovered.
            migrationBuilder.Sql(@"
                INSERT INTO ""MedicalRecordRevisions""
                    (""MedicalRecordId"", ""Revision"", ""Diagnosis"", ""Treatment"", ""Notes"",
                     ""ChangedFields"", ""EditedByDoctorId"", ""EditedByUserName"", ""EditedAt"")
                SELECT ""Id"", 1, ""Diagnosis"", ""Treatment"", ""Notes"", '', ""DoctorId"", NULL, ""CreatedAt""
                FROM ""MedicalRecords"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MedicalRecords_Doctors_LastEditedByDoctorId",
                table: "MedicalRecords");

            migrationBuilder.DropTable(
                name: "MedicalRecordRevisions");

            migrationBuilder.DropIndex(
                name: "IX_MedicalRecords_LastEditedByDoctorId",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "LastEditedByDoctorId",
                table: "MedicalRecords");
        }
    }
}
