using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HospitalManagement.E2E.Support;

/// <summary>
/// Calls the deployed API with a real Keycloak access token taken from a browser sign-in. Used for journeys the
/// frontend does not offer yet (appointments, medical records) — TEST_STRATEGY §9.
/// </summary>
public sealed class HospitalApi : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public HospitalApi(string accessToken)
    {
        _http = new HttpClient { BaseAddress = new Uri(E2EConfig.ApiUrl) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(string path)
    {
        var response = await _http.GetAsync(path);
        return (response.StatusCode, await ReadAsync(response));
    }

    public async Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(string path, object body)
    {
        var response = await _http.PostAsJsonAsync(path, body, Json);
        return (response.StatusCode, await ReadAsync(response));
    }

    public async Task<(HttpStatusCode Status, JsonElement Body)> PutAsync(string path, object body)
    {
        var response = await _http.PutAsJsonAsync(path, body, Json);
        return (response.StatusCode, await ReadAsync(response));
    }

    public async Task<HttpStatusCode> DeleteAsync(string path) => (await _http.DeleteAsync(path)).StatusCode;

    /// <summary>The body as JSON, or an empty element for empty or non-JSON bodies.</summary>
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text) || response.Content.Headers.ContentType?.MediaType?.Contains("json") != true)
            return default;
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public void Dispose() => _http.Dispose();
}
