using System.Net;
using System.Net.Http.Json;
using HospitalManagement.E2E.Support;
using Microsoft.Playwright;
using Reqnroll;

namespace HospitalManagement.E2E.Steps;

[Binding]
public sealed class SignInSteps : IDisposable
{
    private readonly BrowserSession _browser;
    private readonly KeycloakAdmin _keycloak = new();
    private (string Id, string UserName, string Password)? _temporaryAccount;

    public SignInSteps(BrowserSession browser) => _browser = browser;

    [Given("I open the web application")]
    [When("I open the web application")]
    public Task OpenApp() => _browser.OpenAppAsync();

    [Then("I am asked to sign in on the Keycloak page")]
    public Task ExpectSignIn() => _browser.ExpectSignInPageAsync();

    [When("I sign in as {string}")]
    public Task SignIn(string user) => _browser.SignInAsync(user, E2EConfig.UsersPassword);

    [Given("I am signed in as {string}")]
    public Task SignedInAs(string user) => _browser.SignInFromStartAsync(user, E2EConfig.UsersPassword);

    [Then("the application shows {string} as the signed-in user")]
    public Task ExpectName(string name) => Assertions.Expect(_browser.SignedInName).ToHaveTextAsync(name);

    [Then("the sign-in page offers no {string} link")]
    public Task ExpectNoLink(string text) =>
        Assertions.Expect(_browser.Page.GetByRole(AriaRole.Link, new() { Name = text })).ToHaveCountAsync(0);

    [When("I sign out")]
    public Task SignOut() => _browser.SignOutAsync();

    [Given("a Keycloak account without a hospital role")]
    public async Task AccountWithoutRole() => _temporaryAccount = await _keycloak.CreateUserWithoutRoleAsync();

    [When("that account signs in to the web application")]
    public Task TemporaryAccountSignsIn() =>
        _browser.SignInFromStartAsync(_temporaryAccount!.Value.UserName, _temporaryAccount.Value.Password);

    [Then("the application shows {string}")]
    public Task ExpectText(string text) => Assertions.Expect(_browser.Page.GetByText(text, new() { Exact = true })).ToBeVisibleAsync();

    [Then("the API refuses that account's token with 403")]
    public async Task ApiRefusesTemporaryAccount()
    {
        // The "No access" screen makes no API call, so obtain the account's real token from its Keycloak session.
        using var api = new HospitalApi(await TokenFromAuthorizationCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync("/api/User/me")).Status);
    }

    /// <summary>
    /// A token for the temporary account, obtained the way the frontend does it: the browser keeps the Keycloak
    /// session, so a fresh authorization-code round trip in this context returns a token without a password prompt.
    /// </summary>
    private async Task<string> TokenFromAuthorizationCodeAsync()
    {
        var page = _browser.Page;
        var verifier = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var redirect = E2EConfig.FrontendUrl + "/";
        var authorize = $"{E2EConfig.KeycloakUrl}/realms/{E2EConfig.Realm}/protocol/openid-connect/auth" +
                        $"?client_id=hospital-web&response_type=code&scope=openid&code_challenge_method=S256" +
                        $"&code_challenge={challenge}&redirect_uri={Uri.EscapeDataString(redirect)}";

        // Read the code from the request Keycloak redirects the browser to (request events also fire for redirects;
        // route handlers do not).
        var redirected = await page.RunAndWaitForRequestAsync(
            async () => await page.GotoAsync(authorize),
            r => r.Url.StartsWith(E2EConfig.FrontendUrl, StringComparison.Ordinal) && r.Url.Contains("code="));
        var code = System.Web.HttpUtility.ParseQueryString(new Uri(redirected.Url).Query)["code"];
        Assert.False(string.IsNullOrEmpty(code), "Keycloak returned no authorization code");

        using var http = new HttpClient();
        var response = await http.PostAsync(
            $"{E2EConfig.KeycloakUrl}/realms/{E2EConfig.Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code", ["client_id"] = "hospital-web", ["code"] = code!,
                ["redirect_uri"] = redirect, ["code_verifier"] = verifier
            }));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return json.GetProperty("access_token").GetString()!;
    }

    public void Dispose()
    {
        if (_temporaryAccount != null) _keycloak.DeleteUserAsync(_temporaryAccount.Value.Id).GetAwaiter().GetResult();
        _keycloak.Dispose();
    }
}
