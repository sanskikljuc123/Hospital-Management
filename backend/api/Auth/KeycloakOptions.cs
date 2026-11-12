namespace api.Auth
{
    /// <summary>Bound from the "Keycloak" configuration section.</summary>
    public class KeycloakOptions
    {
        public const string SectionName = "Keycloak";

        /// <summary>
        /// Realm URL the API uses to download signing keys, e.g. http://keycloak:8080/realms/HospitalRealm.
        /// Inside Docker this is the internal address of Keycloak.
        /// </summary>
        public string Authority { get; set; } = string.Empty;

        /// <summary>
        /// Realm URL as seen by browsers; this is the "iss" claim of issued tokens.
        /// Defaults to <see cref="Authority"/> when both are the same.
        /// </summary>
        public string? Issuer { get; set; }

        /// <summary>Expected "aud" claim; added to tokens by an audience mapper on the client.</summary>
        public string Audience { get; set; } = "hospital-api";

        public bool RequireHttpsMetadata { get; set; } = true;

        /// <summary>Public client used by Swagger UI to sign in.</summary>
        public string SwaggerClientId { get; set; } = "hospital-web";

        public string PublicAuthority => (string.IsNullOrWhiteSpace(Issuer) ? Authority : Issuer).TrimEnd('/');
    }
}
