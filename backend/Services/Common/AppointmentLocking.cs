using Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Services.Common
{
    /// <summary>
    /// Serializes changes that decide on the same appointment. Writing a medical record for a visit and changing
    /// the visit's status each check the other's rule ("not NoShow" / "no record yet"); without a lock, two such
    /// requests at the same moment both pass their check and both save (A-006). Every request that changes an
    /// appointment's status, or attaches a record to it, first locks the appointment's row
    /// (<c>SELECT … FOR UPDATE</c>); the second request waits until the first commits and then sees its result.
    /// The lock lasts until the request's transaction ends (<c>TransactionalWriteFilter</c>, D-8).
    /// </summary>
    public static class AppointmentLocking
    {
        public static async Task<Appointment?> FindAppointmentForUpdateAsync(this ApplicationDbContext context, int id,
            CancellationToken cancellationToken)
        {
            // The in-memory provider used by unit tests has no row locks; it runs one request at a time anyway.
            if (!context.Database.IsRelational())
                return await context.Appointments.FindAsync(new object[] { id }, cancellationToken);

            // Not composed with further operators, so the locking clause stays at the top level of the query.
            var rows = await context.Appointments
                .FromSqlInterpolated($"SELECT * FROM \"Appointments\" WHERE \"Id\" = {id} FOR UPDATE")
                .ToListAsync(cancellationToken);
            return rows.SingleOrDefault();
        }
    }
}
