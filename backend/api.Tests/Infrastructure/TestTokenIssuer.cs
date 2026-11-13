using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace api.Tests.Infrastructure;

/// <summary>
/// Issues access tokens shaped like Keycloak's (realm roles under "realm_access.roles").
/// The API validates them with its real pipeline: signature, issuer, audience and lifetime are
/// all checked. Only the source of the signing key differs from production.
/// </summary>
public sealed class TestTokenIssuer
{
    public const string Issuer = "https://identity.test.invalid/realms/HospitalRealm";
    public const string Audience = "hospital-api";

    private readonly RsaSecurityKey _key = new(RSA.Create(2048)) { KeyId = "test-signing-key" };
    private readonly RsaSecurityKey _foreignKey = new(RSA.Create(2048)) { KeyId = "test-signing-key" };
    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>The public key the API is configured to trust.</summary>
    public SecurityKey SigningKey => new RsaSecurityKey(_key.Rsa.ExportParameters(false)) { KeyId = _key.KeyId };

    public string Issue(TestIdentity identity, TokenOptions? options = null)
    {
        options ??= new TokenOptions();
        var now = DateTime.UtcNow;

        var claims = new Dictionary<string, object>
        {
            ["sub"] = identity.Subject,
            ["preferred_username"] = identity.Name,
            ["email"] = $"{identity.Name}@example.test",
            ["given_name"] = "Test",
            ["family_name"] = identity.Name,
            ["sid"] = identity.SessionId,
            ["realm_access"] = new Dictionary<string, object> { ["roles"] = options.Roles ?? identity.Roles }
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer ?? Issuer,
            Audience = options.OmitAudience ? null : options.Audience ?? Audience,
            IssuedAt = options.NotBefore ?? now.AddMinutes(-1),
            NotBefore = options.NotBefore ?? now.AddMinutes(-1),
            Expires = options.Expires ?? now.AddMinutes(10),
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                options.SignWithForeignKey ? _foreignKey : _key, SecurityAlgorithms.RsaSha256)
        };

        return _handler.CreateToken(descriptor);
    }

    /// <summary>An unsigned token ("alg": "none") carrying otherwise valid claims.</summary>
    public string IssueUnsigned(TestIdentity identity)
    {
        var now = DateTimeOffset.UtcNow;
        var header = JsonSerializer.Serialize(new { alg = "none", typ = "JWT" });
        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["iss"] = Issuer,
            ["aud"] = Audience,
            ["sub"] = identity.Subject,
            ["preferred_username"] = identity.Name,
            ["sid"] = identity.SessionId,
            ["nbf"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(10).ToUnixTimeSeconds(),
            ["realm_access"] = new Dictionary<string, object> { ["roles"] = identity.Roles }
        });
        return $"{Base64Url(header)}.{Base64Url(payload)}.";
    }

    /// <summary>Replaces the payload of a validly signed token, keeping the original signature.</summary>
    public string TamperRoles(string token, params string[] roles)
    {
        var parts = token.Split('.');
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Base64UrlEncoder.Decode(parts[1]))!;
        var tampered = payload.ToDictionary(p => p.Key, p => (object)p.Value);
        tampered["realm_access"] = new Dictionary<string, object> { ["roles"] = roles };
        return $"{parts[0]}.{Base64Url(JsonSerializer.Serialize(tampered))}.{parts[2]}";
    }

    private static string Base64Url(string json) => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));
}

public sealed record TokenOptions
{
    public string? Issuer { get; init; }
    public string? Audience { get; init; }
    public bool OmitAudience { get; init; }
    public DateTime? NotBefore { get; init; }
    public DateTime? Expires { get; init; }
    public bool SignWithForeignKey { get; init; }
    public string[]? Roles { get; init; }
}
