using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace HospitalManagement.ApiTests.Infrastructure;

/// <summary>
/// Issues tokens shaped like Keycloak's (issuer, audience, sub, preferred_username, sid,
/// realm_access.roles), signed with an RSA key generated for this test run. The API under test trusts
/// only this key (<see cref="ApiFactory"/>), never a real Keycloak.
/// </summary>
public static class TestTokenIssuer
{
    public const string Issuer = "http://keycloak.test/realms/HospitalRealm";
    public const string Audience = "hospital-api";

    public static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048)) { KeyId = "test-key" };

    /// <summary>A key the API does not trust, for forged-token tests.</summary>
    public static readonly RsaSecurityKey UntrustedKey = new(RSA.Create(2048)) { KeyId = "untrusted-key" };

    public static string Issue(
        TestUser user,
        string issuer = Issuer,
        string audience = Audience,
        DateTime? notBefore = null,
        DateTime? expires = null,
        SecurityKey? signingKey = null,
        string? sessionId = null)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new("sub", user.Subject),
            new("preferred_username", user.UserName),
            new("given_name", "Test"),
            new("family_name", user.UserName),
            new("email", $"{user.UserName}@hospital.test"),
            new("sid", sessionId ?? "session-1"),
            new("realm_access", JsonSerializer.Serialize(new { roles = user.Roles }), JsonClaimValueTypes.Json),
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore ?? now.AddMinutes(-1),
            IssuedAt = (notBefore ?? now.AddMinutes(-1)),
            Expires = expires ?? now.AddMinutes(15),
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256),
        };

        var handler = new JwtSecurityTokenHandler { SetDefaultTimesOnTokenCreation = false };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    /// <summary>An unsigned token ("alg": "none") with otherwise valid claims.</summary>
    public static string IssueUnsigned(TestUser user)
    {
        var signed = Issue(user);
        var parts = signed.Split('.');
        var header = Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        return $"{header}.{parts[1]}.";
    }
}
