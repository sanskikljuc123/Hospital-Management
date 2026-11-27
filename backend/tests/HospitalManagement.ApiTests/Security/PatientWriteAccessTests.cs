using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagement.ApiTests.Infrastructure;

namespace HospitalManagement.ApiTests.Security;

/// <summary>
/// Who may register and edit patients (BUSINESS_FLOWS F-02; RISK_REGISTER Q-017, Q-026, Q-035): an administrator or a
/// nurse. A doctor, a patient or a user without a hospital role gets 403 with an empty body, for an existing or a
/// missing id, and nothing is saved or audited (F-02.4, F-02.6).
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public class PatientWriteAccessTests
{
    private const int MissingId = 999_999;
    private readonly ApiFactory _factory;
    private readonly TestData _data;

    public PatientWriteAccessTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
        _data = new TestData(_factory);
    }

    // ---------- Doctor: refused, nothing written (F-02.6) ----------

    [Theory]
    [InlineData("linkedDoctor")]
    [InlineData("unlinkedDoctor")]
    [Trait("Risk", "R-069")]
    [Trait("Scenario", "F-02.6")]
    public async Task CreatePatient_AsDoctor_ValidBody_Returns403_AndCreatesNothing(string kind) =>
        await AssertCreateRefusedAsync(await RefusedUserAsync(kind));

    [Theory]
    [InlineData("linkedDoctor")]
    [InlineData("unlinkedDoctor")]
    [Trait("Risk", "R-069")]
    [Trait("Scenario", "F-02.6")]
    public async Task UpdatePatient_AsDoctor_ExistingPatient_Returns403_AndChangesNothing(string kind) =>
        await AssertUpdateRefusedAsync(await RefusedUserAsync(kind));

    [Theory]
    [InlineData("linkedDoctor")]
    [InlineData("unlinkedDoctor")]
    [Trait("Risk", "R-069")]
    [Trait("Scenario", "F-02.6")]
    public async Task UpdatePatient_AsDoctor_MissingId_Returns403LikeAnExistingId(string kind)
    {
        using var client = _factory.ClientFor(await RefusedUserAsync(kind));

        var response = await client.PutAsJsonAsync($"/api/Patient/{MissingId}",
            await BodyAsync("Missing Patient", "missing@hospital.test"), TestData.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    // ---------- Patient and no role: refused, nothing written (F-02.4) ----------

    [Theory]
    [InlineData("linkedPatient")]
    [InlineData("unlinkedPatient")]
    [InlineData("noRole")]
    [Trait("Risk", "R-069")]
    [Trait("Scenario", "F-02.4")]
    public async Task CreatePatient_AsPatientOrNoRole_ValidBody_Returns403_AndCreatesNothing(string kind) =>
        await AssertCreateRefusedAsync(await RefusedUserAsync(kind));

    [Theory]
    [InlineData("linkedPatient")]
    [InlineData("unlinkedPatient")]
    [InlineData("noRole")]
    [Trait("Risk", "R-069")]
    [Trait("Scenario", "F-02.4")]
    public async Task UpdatePatient_AsPatientOrNoRole_ExistingPatient_Returns403_AndChangesNothing(string kind) =>
        await AssertUpdateRefusedAsync(await RefusedUserAsync(kind));

    // ---------- Admin and Nurse keep access (F-02.1) ----------

    [Theory]
    [InlineData("Admin")]
    [InlineData("Nurse")]
    [Trait("Risk", "R-069")]
    [Trait("Scenario", "F-02.1")]
    public async Task CreatePatient_AsAdminOrNurse_Returns201_AndStoresThePatient(string role)
    {
        var tag = Tag();
        var staff = TestUser.Create("staff", role);
        using var client = _factory.ClientFor(staff);

        var response = await client.PostAsJsonAsync("/api/Patient",
            await BodyAsync($"Test Patient R069 {tag}", $"r069.{tag}@hospital.test"), TestData.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(TestData.Json)).GetProperty("id").GetInt32();
        using var admin = _data.Admin;
        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/Patient/{id}", TestData.Json);
        Assert.Equal($"Test Patient R069 {tag}", stored.GetProperty("userName").GetString());
        Assert.Equal($"r069.{tag}@hospital.test", stored.GetProperty("email").GetString());
        var entries = await admin.GetFromJsonAsync<JsonElement>($"/api/AuditLog?entityType=Patient&entityId={id}", TestData.Json);
        Assert.Contains("Patient.Created", entries.EnumerateArray().Select(e => e.GetProperty("action").GetString()));
        // Positive control for the refused-create check (AssertCreateRefusedAsync): the same audit query, by action and
        // the caller's subject, finds this user's entry. Otherwise its "no entry" could pass on a broken filter.
        var byCaller = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/AuditLog?action=Patient.Created&userId={Uri.EscapeDataString(staff.Subject)}", TestData.Json);
        Assert.Equal(id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Assert.Single(byCaller.EnumerateArray()).GetProperty("entityId").GetString());
    }

    [Fact]
    [Trait("Risk", "R-069")]
    [Trait("Scenario", "F-02.1")]
    public async Task UpdatePatient_AsNurse_Returns200_AndStoresTheChange()
    {
        var patientId = await _data.CreatePatientAsync();
        var tag = Tag();
        using var nurse = _factory.ClientFor(TestUser.Nurse());

        var response = await nurse.PutAsJsonAsync($"/api/Patient/{patientId}",
            await BodyAsync($"Renamed Patient R069 {tag}", $"renamed.{tag}@hospital.test"), TestData.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var admin = _data.Admin;
        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/Patient/{patientId}", TestData.Json);
        Assert.Equal($"Renamed Patient R069 {tag}", stored.GetProperty("userName").GetString());
        Assert.Equal($"renamed.{tag}@hospital.test", stored.GetProperty("email").GetString());
    }

    // ---------- One allowed role is enough (Q-026, Q-035) ----------

    [Fact]
    [Trait("Risk", "R-069")]
    [Trait("Scenario", "F-02.1")]
    public async Task CreatePatient_AsDoctorAndNurse_Returns201_AndStoresThePatient()
    {
        var tag = Tag();
        using var client = _factory.ClientFor(TestUser.Create("doctornurse", "Doctor", "Nurse"));

        var response = await client.PostAsJsonAsync("/api/Patient",
            await BodyAsync($"Test Patient R069 {tag}", $"r069.{tag}@hospital.test"), TestData.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(TestData.Json)).GetProperty("id").GetInt32();
        using var admin = _data.Admin;
        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/Patient/{id}", TestData.Json);
        Assert.Equal($"Test Patient R069 {tag}", stored.GetProperty("userName").GetString());
    }

    [Fact]
    [Trait("Risk", "R-069")]
    [Trait("Scenario", "F-02.1")]
    public async Task UpdatePatient_AsDoctorAndNurse_Returns200_AndStoresTheChange()
    {
        var patientId = await _data.CreatePatientAsync();
        var tag = Tag();
        using var client = _factory.ClientFor(TestUser.Create("doctornurse", "Doctor", "Nurse"));

        var response = await client.PutAsJsonAsync($"/api/Patient/{patientId}",
            await BodyAsync($"Renamed Patient R069 {tag}", $"renamed.{tag}@hospital.test"), TestData.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var admin = _data.Admin;
        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/Patient/{patientId}", TestData.Json);
        Assert.Equal($"Renamed Patient R069 {tag}", stored.GetProperty("userName").GetString());
    }

    // ---------- Shared checks ----------

    /// <summary>A refused create: 403, empty body, no such patient stored, no Patient.Created entry by this user.</summary>
    private async Task AssertCreateRefusedAsync(TestUser user)
    {
        var tag = Tag();
        var userName = $"Test Patient R069 {tag}";
        using var client = _factory.ClientFor(user);

        var response = await client.PostAsJsonAsync("/api/Patient",
            await BodyAsync(userName, $"r069.{tag}@hospital.test"), TestData.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        using var admin = _data.Admin;
        var patients = await admin.GetFromJsonAsync<JsonElement>("/api/Patient", TestData.Json);
        Assert.DoesNotContain(patients.EnumerateArray(), p => p.GetProperty("userName").GetString() == userName);
        var created = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/AuditLog?action=Patient.Created&userId={Uri.EscapeDataString(user.Subject)}", TestData.Json);
        Assert.Empty(created.EnumerateArray());
    }

    /// <summary>A refused update of an existing patient: 403, empty body, the stored patient unchanged, only its creation audited.</summary>
    private async Task AssertUpdateRefusedAsync(TestUser user)
    {
        var patientId = await _data.CreatePatientAsync();
        using var admin = _data.Admin;
        var before = (await admin.GetFromJsonAsync<JsonElement>($"/api/Patient/{patientId}", TestData.Json)).GetRawText();
        using var client = _factory.ClientFor(user);

        var response = await client.PutAsJsonAsync($"/api/Patient/{patientId}",
            await BodyAsync("Changed By A Refused Role", "changed.refused@hospital.test"), TestData.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        var after = (await admin.GetFromJsonAsync<JsonElement>($"/api/Patient/{patientId}", TestData.Json)).GetRawText();
        Assert.Equal(before, after);
        var entries = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/AuditLog?entityType=Patient&entityId={patientId}", TestData.Json);
        Assert.Equal(new[] { "Patient.Created" },
            entries.EnumerateArray().Select(e => e.GetProperty("action").GetString()).ToArray());
    }

    private async Task<TestUser> RefusedUserAsync(string kind) => kind switch
    {
        "linkedDoctor" => (await _data.LinkedDoctorAsync()).User,
        "unlinkedDoctor" => TestUser.Doctor(),
        "linkedPatient" => (await _data.LinkedPatientAsync()).User,
        "unlinkedPatient" => TestUser.Patient(),
        "noRole" => TestUser.NoRole(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private async Task<object> BodyAsync(string userName, string email) =>
        new { userName, phone = 40000000, email, serviceId = await _data.ServiceIdAsync() };

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];
}
