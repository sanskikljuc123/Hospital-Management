using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace api.Infrastructure
{
    /// <summary>
    /// Turns database constraint violations (for example deleting a patient who still has
    /// appointments) into 409 Conflict instead of an unhandled 500.
    /// </summary>
    public class DbUpdateExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<DbUpdateExceptionHandler> _logger;

        public DbUpdateExceptionHandler(ILogger<DbUpdateExceptionHandler> logger) => _logger = logger;

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is not DbUpdateException) return false;

            _logger.LogWarning(exception, "Database update rejected for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

            await Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The change conflicts with existing data",
                detail: "The record is referenced by other data or violates a uniqueness rule.").ExecuteAsync(httpContext);
            return true;
        }
    }
}
