using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace HospitalManagement.E2E.Support;

/// <summary>
/// Creates and deletes a temporary Keycloak user without a hospital role (BUSINESS_FLOWS F-01.6). Uses the Keycloak
/// admin API of the master realm; the hospital realm itself allows no password grant (R-027).
/// </summary>
public sealed class KeycloakAdmin : IDisposable
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri(E2EConfig.KeycloakUrl) };

    public async Task<(string Id, string UserName, string Password)> CreateUserWithoutRoleAsync()
    {
        await AuthenticateAsync();
        var userName = "e2e.norole." + Guid.NewGuid().ToString("N")[..8];
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        var response = await _http.PostAsJsonAsync($"/admin/realms/{E2EConfig.Realm}/users", new
        {
            username = userName,
            enabled = true,
            email = $"{userName}@hospital.test",
            emailVerified = true,
            firstName = "No",
            lastName = "Role",
            credentials = new[] { new { type = "password", value = password, temporary = false } }
        });
        response.EnsureSuccessStatusCode();
        var id = response.Headers.Location!.Segments.Last();
        return (id, userName, password);
    }

    public async Task DeleteUserAsync(string id)
    {
        await AuthenticateAsync();
        (await _http.DeleteAsync($"/admin/realms/{E2EConfig.Realm}/users/{id}")).EnsureSuccessStatusCode();
    }

    private async Task AuthenticateAsync()
    {
        var response = await _http.PostAsync("/realms/master/protocol/openid-connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = E2EConfig.KeycloakAdmin,
                ["password"] = E2EConfig.KeycloakAdminPassword
            }));
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void Dispose() => _http.Dispose();
}
