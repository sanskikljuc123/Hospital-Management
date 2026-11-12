using System.Security.Claims;
using Services.Common;

namespace api.Auth
{
    public class HttpCurrentUserContext : ICurrentUserContext
    {
        private readonly IHttpContextAccessor _accessor;

        public HttpCurrentUserContext(IHttpContextAccessor accessor) => _accessor = accessor;

        private ClaimsPrincipal? User => _accessor.HttpContext?.User;

        public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
        public string? Subject => User?.FindFirst("sub")?.Value;
        public string? UserName => User?.FindFirst("preferred_username")?.Value;
        public string? Email => User?.FindFirst("email")?.Value;
        public string? FirstName => User?.FindFirst("given_name")?.Value;
        public string? LastName => User?.FindFirst("family_name")?.Value;
        public string? SessionId => User?.FindFirst("sid")?.Value;
        public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        public IReadOnlyCollection<string> Roles =>
            User?.FindAll(AuthenticationExtensions.RoleClaimType).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

        public bool IsInRole(string role) => User?.IsInRole(role) == true;
    }
}
