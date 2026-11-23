using System.Net;
using HospitalManagement.ApiTests.Infrastructure;

namespace HospitalManagement.ApiTests.Security;

/// <summary>docs/testing/SECURITY_TESTING.md §4: every malformed or untrusted token gets 401.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
[Trait("Risk", "R-006")]
public class TokenValidationTests
{
    private const string ProtectedPath = "/api/Doctor";
    private readonly ApiFactory _factory;

    public TokenValidationTests(ApiFixture fixture) => _factory = fixture.Factory;

    public static IEnumerable<object[]> InvalidAuthorizationHeaders()
    {
        var user = TestUser.Admin();
        var now = DateTime.UtcNow;
        yield return new object[] { "no header", null! };
        yield return new object[] { "empty bearer", "Bearer " };
        yield return new object[] { "not a JWT", "Bearer not-a-token" };
        yield return new object[] { "expired beyond clock skew",
            "Bearer " + TestTokenIssuer.Issue(user, notBefore: now.AddHours(-2), expires: now.AddMinutes(-5)) };
        yield return new object[] { "not yet valid",
            "Bearer " + TestTokenIssuer.Issue(user, notBefore: now.AddMinutes(10), expires: now.AddMinutes(30)) };
        yield return new object[] { "wrong issuer",
            "Bearer " + TestTokenIssuer.Issue(user, issuer: "http://evil.test/realms/HospitalRealm") };
        yield return new object[] { "wrong audience", "Bearer " + TestTokenIssuer.Issue(user, audience: "other-api") };
        yield return new object[] { "untrusted signing key",
            "Bearer " + TestTokenIssuer.Issue(user, signingKey: TestTokenIssuer.UntrustedKey) };
        yield return new object[] { "signature removed", "Bearer " + RemoveSignature(TestTokenIssuer.Issue(user)) };
        yield return new object[] { "alg none", "Bearer " + TestTokenIssuer.IssueUnsigned(user) };
    }

    [Theory]
    [MemberData(nameof(InvalidAuthorizationHeaders))]
    public async Task ProtectedEndpoint_InvalidToken_Returns401(string _, string? authorization)
    {
        using var client = _factory.ClientWithAuthorization(authorization);

        var response = await client.GetAsync(ProtectedPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Suite", "Smoke")]
    public async Task ProtectedEndpoint_ValidToken_Returns200()
    {
        using var client = _factory.ClientFor(TestUser.Admin());

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(ProtectedPath)).StatusCode);
    }

    private static string RemoveSignature(string token) => token[..(token.LastIndexOf('.') + 1)];
}
