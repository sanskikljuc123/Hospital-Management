using Microsoft.Playwright;

namespace HospitalManagement.E2E.Support;

/// <summary>
/// One browser context per scenario (fresh cookies, fresh Keycloak session). Every wait is on a condition —
/// a URL, an element, a request — never a fixed delay (TEST_STRATEGY §8).
/// </summary>
public sealed class BrowserSession
{
    public BrowserSession(IPage page) => Page = page;

    public IPage Page { get; }

    public async Task OpenAppAsync(string path = "/") =>
        await Page.GotoAsync(E2EConfig.FrontendUrl + path);

    public ILocator KeycloakUsername => Page.Locator("#username");

    /// <summary>Waits until the browser shows the Keycloak sign-in page of the hospital realm.</summary>
    public async Task ExpectSignInPageAsync()
    {
        await Page.WaitForURLAsync(url => url.StartsWith($"{E2EConfig.KeycloakUrl}/realms/{E2EConfig.Realm}/"));
        await Assertions.Expect(KeycloakUsername).ToBeVisibleAsync();
    }

    /// <summary>Fills the real Keycloak login form and waits until the app is back on the frontend.</summary>
    public async Task SignInAsync(string username, string password)
    {
        await ExpectSignInPageAsync();
        await KeycloakUsername.FillAsync(username);
        await Page.Locator("#password").FillAsync(password);
        await Page.Locator("#kc-login").ClickAsync();
        await Page.WaitForURLAsync(url => url.StartsWith(E2EConfig.FrontendUrl));
    }

    public async Task SignInFromStartAsync(string username, string password)
    {
        await OpenAppAsync();
        await SignInAsync(username, password);
    }

    /// <summary>The name shown in the navigation bar.</summary>
    public ILocator SignedInName => Page.Locator(".header--user-name");

    public async Task SignOutAsync() => await Page.GetByText("Logout", new() { Exact = true }).ClickAsync();

    /// <summary>
    /// The real access token the frontend sends to the API: opens a page that calls the API and reads the
    /// Authorization header of that request. The token stays in memory in the app (ARCHITECTURE §3).
    /// </summary>
    public async Task<string> CaptureAccessTokenAsync()
    {
        var request = await Page.RunAndWaitForRequestAsync(
            async () => await Page.GotoAsync(E2EConfig.FrontendUrl + "/doctor"),
            r => r.Url.StartsWith(E2EConfig.ApiUrl + "/api/") && r.Headers.ContainsKey("authorization"));
        var header = request.Headers["authorization"];
        return header.StartsWith("Bearer ") ? header["Bearer ".Length..] : throw new InvalidOperationException("No bearer token");
    }
}
