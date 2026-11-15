using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HospitalManagement.ApiTests.Infrastructure;

/// <summary>
/// Builds synthetic data through the public API, as an administrator or staff member would. Nothing is
/// inserted behind the API's back, so every helper also exercises the real endpoints.
/// Values follow the synthetic data rules (docs/testing/BUSINESS_FLOWS.md §4).
/// </summary>
public sealed class TestData
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;
    private readonly TestUser _admin = TestUser.Admin();
    private int? _serviceId;

    public TestData(ApiFactory factory) => _factory = factory;

    public HttpClient Admin => _factory.ClientFor(_admin);

    /// <summary>First call of a user: the API creates the local profile. Returns the profile id.</summary>
    public async Task<int> ProvisionAsync(TestUser user)
    {
        using var client = _factory.ClientFor(user);
        (await client.GetAsync("/api/User/me")).EnsureSuccessStatusCode();
        using var admin = Admin;
        var users = await admin.GetFromJsonAsync<JsonElement>("/api/User", Json);
        return users.EnumerateArray().Single(u => u.GetProperty("userName").GetString() == user.UserName)
            .GetProperty("id").GetInt32();
    }

    public async Task<int> ServiceIdAsync()
    {
        if (_serviceId.HasValue) return _serviceId.Value;
        using var admin = Admin;
        var response = await admin.PostAsJsonAsync("/api/Service",
            new { name = "Test service " + Guid.NewGuid().ToString("N")[..6], description = "synthetic" }, Json);
        response.EnsureSuccessStatusCode();
        _serviceId = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetInt32();
        return _serviceId.Value;
    }

    public async Task<int> CreatePatientAsync()
    {
        using var admin = Admin;
        var tag = Guid.NewGuid().ToString("N")[..8];
        var response = await admin.PostAsJsonAsync("/api/Patient", new
        {
            userName = $"Test Patient {tag}",
            phone = 10000000,
            email = $"patient.{tag}@hospital.test",
            serviceId = await ServiceIdAsync()
        }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetInt32();
    }

    public async Task<int> CreateDoctorAsync()
    {
        using var admin = Admin;
        var tag = Guid.NewGuid().ToString("N")[..8];
        var response = await admin.PostAsJsonAsync("/api/Doctor", new
        {
            firstName = "Test",
            lastName = $"Doctor {tag}",
            birthday = "1980-01-01",
            address = "1 Test Street",
            serviceIds = Array.Empty<int>()
        }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetInt32();
    }

    public async Task LinkAsync(TestUser user, int? doctorId = null, int? patientId = null)
    {
        var userId = await ProvisionAsync(user);
        using var admin = Admin;
        var response = await admin.PutAsJsonAsync($"/api/User/{userId}/link", new { doctorId, patientId }, Json);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>A patient account linked to a new patient record.</summary>
    public async Task<(TestUser User, int PatientId)> LinkedPatientAsync()
    {
        var user = TestUser.Patient();
        var patientId = await CreatePatientAsync();
        await LinkAsync(user, patientId: patientId);
        return (user, patientId);
    }

    /// <summary>A doctor account linked to a new doctor profile.</summary>
    public async Task<(TestUser User, int DoctorId)> LinkedDoctorAsync()
    {
        var user = TestUser.Doctor();
        var doctorId = await CreateDoctorAsync();
        await LinkAsync(user, doctorId: doctorId);
        return (user, doctorId);
    }

    /// <summary>Books an appointment as staff, <paramref name="daysAhead"/> days from now at 09:00 UTC.</summary>
    public async Task<int> CreateAppointmentAsync(int patientId, int doctorId, int daysAhead = 7)
    {
        using var admin = Admin;
        var start = DateTime.UtcNow.Date.AddDays(daysAhead).AddHours(9);
        var response = await admin.PostAsJsonAsync("/api/Appointment", new
        {
            patientId, doctorId, scheduledAt = start, durationMinutes = 30, reason = "Synthetic check-up"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetInt32();
    }

    public async Task<int> CreateRecordAsync(TestUser doctor, int patientId, string diagnosis = "Synthetic diagnosis")
    {
        using var client = _factory.ClientFor(doctor);
        var response = await client.PostAsJsonAsync("/api/MedicalRecord", new { patientId, diagnosis }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetInt32();
    }
}
