namespace Domain.Entities
{
    /// <summary>
    /// Local profile of a Keycloak identity. Credentials and role assignments live in Keycloak;
    /// this row exists so domain data (doctors, patients, audit) can reference a user.
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        /// <summary>The Keycloak subject ("sub" claim).</summary>
        public string KeycloakId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        /// <summary>Most privileged realm role seen on the latest token.</summary>
        public string? Role { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        /// <summary>Keycloak session id of the last recorded login.</summary>
        public string? LastSessionId { get; set; }
    }
}
