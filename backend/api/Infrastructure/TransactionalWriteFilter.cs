using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace api.Infrastructure
{
    /// <summary>
    /// One database transaction per write request (POST, PUT, PATCH, DELETE): the business change, its audit entry,
    /// its record revision and its outbox messages commit together or not at all (decision D-8). Committed only
    /// when the action succeeds; an exception, a 4xx/5xx result or a challenge/forbid rolls everything back.
    /// </summary>
    public sealed class TransactionalWriteFilter : IAsyncActionFilter
    {
        private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            "POST", "PUT", "PATCH", "DELETE"
        };

        private readonly ApplicationDbContext _context;

        public TransactionalWriteFilter(ApplicationDbContext context) => _context = context;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!WriteMethods.Contains(context.HttpContext.Request.Method) || _context.Database.CurrentTransaction != null)
            {
                await next();
                return;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(context.HttpContext.RequestAborted);
            var executed = await next();

            if (executed.Exception == null && IsSuccess(executed.Result))
                await transaction.CommitAsync(context.HttpContext.RequestAborted);
            else
                await transaction.RollbackAsync(CancellationToken.None);
        }

        private static bool IsSuccess(IActionResult? result) => result switch
        {
            ForbidResult or ChallengeResult => false,
            IStatusCodeActionResult { StatusCode: >= 400 } => false,
            _ => true
        };
    }
}
