using Services.UserService;

namespace api.Auth
{
    /// <summary>
    /// Runs after authentication. Mirrors the token's user into the local Users table, records a
    /// login audit entry for each new Keycloak session, and blocks locally deactivated accounts.
    /// </summary>
    public class UserSyncMiddleware
    {
        private readonly RequestDelegate _next;

        public UserSyncMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, IUserService users)
        {
            if (context.User.Identity?.IsAuthenticated == true
                && !await users.SyncCurrentUserAsync(context.RequestAborted))
            {
                await Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Account deactivated",
                    detail: "This account has been deactivated. Contact an administrator.").ExecuteAsync(context);
                return;
            }

            await _next(context);
        }
    }
}
