namespace Services.Common
{
    /// <summary>
    /// The caller of the current operation, as described by the validated access token.
    /// Implemented by the host (the API reads it from HttpContext).
    /// </summary>
    public interface ICurrentUserContext
    {
        bool IsAuthenticated { get; }
        /// <summary>Keycloak subject ("sub").</summary>
        string? Subject { get; }
        string? UserName { get; }
        string? Email { get; }
        string? FirstName { get; }
        string? LastName { get; }
        /// <summary>Keycloak session id ("sid"); changes on every interactive login.</summary>
        string? SessionId { get; }
        string? IpAddress { get; }
        IReadOnlyCollection<string> Roles { get; }
        bool IsInRole(string role);
    }
}
