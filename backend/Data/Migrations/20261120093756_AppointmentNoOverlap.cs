using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentNoOverlap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndsAt",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Existing rows get their end time before the check constraint is added.
            migrationBuilder.Sql(
                "UPDATE \"Appointments\" SET \"EndsAt\" = \"ScheduledAt\" + make_interval(mins => \"DurationMinutes\");");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_EndsAt",
                table: "Appointments",
                sql: "\"EndsAt\" = \"ScheduledAt\" + make_interval(mins => \"DurationMinutes\")");

            // R-048: one doctor cannot have two appointments whose times overlap, unless one is cancelled.
            // The service checks this too, but two requests at the same moment can both pass that check;
            // the database decides between them. Ranges are half-open, so 09:00-09:30 and 09:30-10:00 do not
            // overlap. btree_gist lets the integer DoctorId share a GiST index with the time range; it is a
            // trusted extension, so the database owner can create it.
            // If this fails on an existing database, overlapping appointments already exist; ARCHITECTURE.md
            // (data integrity rules) has the query that lists them. They must be resolved first.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(
                "ALTER TABLE \"Appointments\" ADD CONSTRAINT \"EX_Appointments_DoctorNoOverlap\" " +
                "EXCLUDE USING gist (\"DoctorId\" WITH =, tstzrange(\"ScheduledAt\", \"EndsAt\", '[)') WITH &&) " +
                "WHERE (\"Status\" <> 'Cancelled');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"Appointments\" DROP CONSTRAINT IF EXISTS \"EX_Appointments_DoctorNoOverlap\";");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_EndsAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "EndsAt",
                table: "Appointments");
        }
    }
}
