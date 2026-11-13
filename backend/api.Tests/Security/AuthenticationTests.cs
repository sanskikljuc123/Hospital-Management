using System.Net;
using System.Net.Http.Headers;
using api.Tests.Infrastructure;
using Domain.Dtos;

namespace api.Tests.Security;

/// <summary>
/// Authentication cases from docs/qe-framework/SECURITY_TESTING.md section 6.
/// Token-validation cases use one representative protected endpoint, as that section allows.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Gate", "G-SEC-01")]
public class AuthenticationTests
{
    private const string ProtectedUrl = "/api/Patient";
    private readonly ApiFactory _api;

    public AuthenticationTests(ApiFactory api) => _api = api;

    private static async Task AssertUnauthorizedWithoutDetailAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // A 401 must not carry data or explain why validation failed in the body.
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact, Trait("Case", "AUTHN-01")]
    public async Task Request_without_authorization_header_is_unauthorized()
    {
        var response = await _api.SendAsync(HttpMethod.Get, ProtectedUrl, identity: null);
        await AssertUnauthorizedWithoutDetailAsync(response);
    }

    [Theory, Trait("Case", "AUTHN-02")]
    [InlineData("not-a-jwt")]
    [InlineData("aaa.bbb.ccc")]
    [InlineData("")]
    public async Task Request_with_malformed_token_is_unauthorized(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedUrl);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        var response = await _api.NewClient().SendAsync(request);
        await AssertUnauthorizedWithoutDetailAsync(response);
    }

    [Fact, Trait("Case", "AUTHN-02")]
    public async Task Request_with_non_bearer_scheme_is_unauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "dGVzdDp0ZXN0");
        var response = await _api.NewClient().SendAsync(request);
        await AssertUnauthorizedWithoutDetailAsync(response);
    }

    [Fact, Trait("Case", "AUTHN-03")]
    public async Task Expired_token_is_unauthorized()
    {
        var token = _api.Tokens.Issue(TestIdentities.Admin1, new TokenOptions
        {
            NotBefore = DateTime.UtcNow.AddMinutes(-20),
            Expires = DateTime.UtcNow.AddMinutes(-5)
        });
        await AssertUnauthorizedWithoutDetailAsync(await _api.SendWithTokenAsync(HttpMethod.Get, ProtectedUrl, token));
    }

    [Fact, Trait("Case", "AUTHN-04")]
    public async Task Token_signed_with_another_key_is_unauthorized()
    {
        var token = _api.Tokens.Issue(TestIdentities.Admin1, new TokenOptions { SignWithForeignKey = true });
        await AssertUnauthorizedWithoutDetailAsync(await _api.SendWithTokenAsync(HttpMethod.Get, ProtectedUrl, token));
    }

    [Fact, Trait("Case", "AUTHN-05")]
    public async Task Unsigned_token_with_algorithm_none_is_unauthorized()
    {
        var token = _api.Tokens.IssueUnsigned(TestIdentities.Admin1);
        await AssertUnauthorizedWithoutDetailAsync(await _api.SendWithTokenAsync(HttpMethod.Get, ProtectedUrl, token));
    }

    [Fact, Trait("Case", "AUTHN-06")]
    public async Task Token_from_another_issuer_is_unauthorized()
    {
        var token = _api.Tokens.Issue(TestIdentities.Admin1,
            new TokenOptions { Issuer = "https://identity.test.invalid/realms/OtherRealm" });
        await AssertUnauthorizedWithoutDetailAsync(await _api.SendWithTokenAsync(HttpMethod.Get, ProtectedUrl, token));
    }

    [Fact, Trait("Case", "AUTHN-07")]
    public async Task Token_for_another_audience_is_unauthorized()
    {
        var token = _api.Tokens.Issue(TestIdentities.Admin1, new TokenOptions { Audience = "some-other-api" });
        await AssertUnauthorizedWithoutDetailAsync(await _api.SendWithTokenAsync(HttpMethod.Get, ProtectedUrl, token));
    }

    [Fact, Trait("Case", "AUTHN-07")]
    public async Task Token_without_audience_is_unauthorized()
    {
        var token = _api.Tokens.Issue(TestIdentities.Admin1, new TokenOptions { OmitAudience = true });
        await AssertUnauthorizedWithoutDetailAsync(await _api.SendWithTokenAsync(HttpMethod.Get, ProtectedUrl, token));
    }

    [Fact, Trait("Case", "AUTHN-08")]
    public async Task Token_not_yet_valid_is_unauthorized()
    {
        var token = _api.Tokens.Issue(TestIdentities.Admin1, new TokenOptions
        {
            NotBefore = DateTime.UtcNow.AddMinutes(10),
            Expires = DateTime.UtcNow.AddMinutes(20)
        });
        await AssertUnauthorizedWithoutDetailAsync(await _api.SendWithTokenAsync(HttpMethod.Get, ProtectedUrl, token));
    }

    [Fact, Trait("Case", "AUTHZ-05")]
    public async Task Token_with_role_claim_altered_after_signing_is_unauthorized()
    {
        var patientToken = _api.Tokens.Issue(TestIdentities.PatientUnlinked);
        var tampered = _api.Tokens.TamperRoles(patientToken, "Admin");
        await AssertUnauthorizedWithoutDetailAsync(await _api.SendWithTokenAsync(HttpMethod.Get, ProtectedUrl, tampered));
    }

    [Fact]
    public async Task Valid_token_is_accepted()
    {
        // Guards the tests above: without this, a broken test issuer would make every case pass.
        var response = await _api.SendAsync(HttpMethod.Get, ProtectedUrl, TestIdentities.Admin1);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact, Trait("Case", "AUTHN-09")]
    public async Task Deactivated_account_is_refused_even_with_a_valid_token()
    {
        // Own identity: deactivation must not affect identities other tests rely on.
        var identity = new TestIdentity("test-deactivated", "00000000-0000-4000-a000-000000000051", "Nurse");
        var user = await _api.ProvisionAsync(identity);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Get, ProtectedUrl, identity)).StatusCode);

        try
        {
            var deactivate = await _api.SendAsync(HttpMethod.Put, $"/api/User/{user.Id}/status",
                TestIdentities.Admin1, new UserStatusDto { IsActive = false });
            Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

            var response = await _api.SendAsync(HttpMethod.Get, ProtectedUrl, identity);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain("synthetic-patient", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            await _api.SendAsync(HttpMethod.Put, $"/api/User/{user.Id}/status",
                TestIdentities.Admin1, new UserStatusDto { IsActive = true });
        }
    }

    [Theory, Trait("Case", "AUTHN-10")]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task Health_endpoints_are_anonymous_and_return_only_a_status_word(string url)
    {
        var response = await _api.SendAsync(HttpMethod.Get, url, identity: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
