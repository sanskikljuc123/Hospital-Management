using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace api.Infrastructure
{
    /// <summary>
    /// Turns database conflicts into 409 Conflict instead of an unhandled 500: constraint violations (for example
    /// deleting a patient who still has appointments, or two overlapping bookings, R-048) and deadlocks between
    /// two requests that change the same data at the same moment.
    /// </summary>
    public class DbUpdateExceptionHandler : IExceptionHandler
    {
        private const string DoctorNoOverlap = "EX_Appointments_DoctorNoOverlap";

        private readonly ILogger<DbUpdateExceptionHandler> _logger;

        public DbUpdateExceptionHandler(ILogger<DbUpdateExceptionHandler> logger) => _logger = logger;

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var postgres = FindPostgresError(exception);
            // EF Core reports a deadlock as a transient failure (InvalidOperationException around the
            // DbUpdateException), so the database error is looked for in the whole chain.
            var deadlock = postgres?.SqlState == PostgresErrorCodes.DeadlockDetected;
            if (exception is not DbUpdateException && !deadlock) return false;

            _logger.LogWarning(exception, "Database update rejected for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

            // Two bookings for one doctor at the same time that both passed the service's check: the database's
            // exclusion constraint refused one (R-048). Same message as the service's own check.
            var slotTaken = postgres is { SqlState: PostgresErrorCodes.ExclusionViolation, ConstraintName: DoctorNoOverlap };

            await Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The change conflicts with existing data",
                detail: slotTaken ? "The doctor already has an appointment in that time slot"
                    : deadlock ? "Another change to the same data was saved at the same moment; reload and try again."
                    : "The record is referenced by other data or violates a uniqueness rule.").ExecuteAsync(httpContext);
            return true;
        }

        private static PostgresException? FindPostgresError(Exception? exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
                if (current is PostgresException postgres) return postgres;
            return null;
        }
    }
}
