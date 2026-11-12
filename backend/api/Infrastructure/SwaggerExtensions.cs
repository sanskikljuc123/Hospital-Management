using api.Auth;
using Microsoft.OpenApi.Models;

namespace api.Infrastructure
{
    public static class SwaggerExtensions
    {
        private const string SchemeName = "keycloak";

        /// <summary>Swagger with an "Authorize" button that signs in through Keycloak (authorization code + PKCE).</summary>
        public static IServiceCollection AddSwaggerWithKeycloak(this IServiceCollection services, IConfiguration configuration)
        {
            var keycloak = configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>() ?? new KeycloakOptions();
            var openIdConnect = $"{keycloak.PublicAuthority}/protocol/openid-connect";

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "Hospital Management API", Version = "v1" });
                options.AddSecurityDefinition(SchemeName, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        AuthorizationCode = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri($"{openIdConnect}/auth"),
                            TokenUrl = new Uri($"{openIdConnect}/token"),
                            Scopes = new Dictionary<string, string>
                            {
                                ["openid"] = "OpenID Connect",
                                ["profile"] = "User profile"
                            }
                        }
                    }
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = SchemeName }
                        },
                        new[] { "openid", "profile" }
                    }
                });
            });
            return services;
        }

        public static WebApplication UseSwaggerWithKeycloak(this WebApplication app)
        {
            var keycloak = app.Services.GetRequiredService<KeycloakOptions>();
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.OAuthClientId(keycloak.SwaggerClientId);
                options.OAuthUsePkce();
                options.OAuthScopes("openid", "profile");
            });
            return app;
        }
    }
}
