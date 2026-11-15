using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace HospitalManagement.ApiTests.Infrastructure;

/// <summary>
/// The real API pipeline (routing, authentication, authorization, user sync, controllers, EF Core) against
/// a real PostgreSQL database. The only replacement: tokens are validated against the test signing key
/// instead of keys downloaded from Keycloak. Issuer, audience, lifetime and role mapping are the API's own.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ApiFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:hospitaldb", _connectionString);
        builder.UseSetting("Keycloak:Authority", TestTokenIssuer.Issuer);
        builder.UseSetting("Keycloak:Issuer", TestTokenIssuer.Issuer);
        builder.UseSetting("Keycloak:Audience", TestTokenIssuer.Audience);
        builder.UseSetting("Keycloak:RequireHttpsMetadata", "false");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:SeedOnStartup", "false");
        builder.UseSetting("Swagger:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = TestTokenIssuer.Issuer };
                configuration.SigningKeys.Add(TestTokenIssuer.SigningKey);
                options.Configuration = configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    }

    /// <summary>A client that sends the given user's token; no token for <c>null</c>.</summary>
    public HttpClient ClientFor(TestUser? user)
    {
        var client = CreateClient();
        if (user != null)
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", TestTokenIssuer.Issue(user));
        return client;
    }

    /// <summary>A client that sends a raw Authorization header value (for tampered tokens).</summary>
    public HttpClient ClientWithAuthorization(string? headerValue)
    {
        var client = CreateClient();
        if (headerValue != null)
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", headerValue);
        return client;
    }
}
