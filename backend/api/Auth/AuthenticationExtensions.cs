using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace api.Auth
{
    public static class AuthenticationExtensions
    {
        /// <summary>Claim type holding one realm role per claim after token validation.</summary>
        public const string RoleClaimType = "roles";

        public static IServiceCollection AddKeycloakAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var keycloak = configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>() ?? new KeycloakOptions();
            if (string.IsNullOrWhiteSpace(keycloak.Authority))
                throw new InvalidOperationException(
                    "Keycloak:Authority is missing. Set it to the realm URL, e.g. http://localhost:8080/realms/HospitalRealm.");

            services.AddSingleton(keycloak);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = keycloak.Authority;
                    options.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;
                    // Keep the original JWT claim names (sub, preferred_username, ...).
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = keycloak.PublicAuthority,
                        ValidateAudience = true,
                        ValidAudience = keycloak.Audience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        NameClaimType = "preferred_username",
                        RoleClaimType = RoleClaimType
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            if (context.Principal?.Identity is ClaimsIdentity identity)
                                AddRealmRoles(identity);
                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddAuthorization(Policies.Configure);
            return services;
        }

        /// <summary>
        /// Keycloak puts realm roles in a nested object: "realm_access": { "roles": [...] }.
        /// ASP.NET Core authorization needs one claim per role, so flatten them.
        /// </summary>
        private static void AddRealmRoles(ClaimsIdentity identity)
        {
            var realmAccess = identity.FindFirst("realm_access")?.Value;
            if (string.IsNullOrEmpty(realmAccess)) return;

            try
            {
                using var document = JsonDocument.Parse(realmAccess);
                if (!document.RootElement.TryGetProperty("roles", out var roles) || roles.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var role in roles.EnumerateArray())
                {
                    var name = role.GetString();
                    if (!string.IsNullOrEmpty(name) && !identity.HasClaim(RoleClaimType, name))
                        identity.AddClaim(new Claim(RoleClaimType, name));
                }
            }
            catch (JsonException)
            {
                // Malformed claim: the user simply ends up with no roles.
            }
        }
    }
}
