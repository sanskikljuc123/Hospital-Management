using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagement.ApiTests.Infrastructure;

namespace HospitalManagement.ApiTests.Catalog;

/// <summary>
/// Doctors, patients and services after moving them to the common service pattern (architecture review C-2):
/// DTOs only, no over-posting, references validated, deletes that never lose data, audit without personal data.
/// </summary>
[Collection(ApiCollection.Name)]
public class CatalogTests
{
    // The fields each list may carry, from contracts/openapi.json. An allowlist, not a list of forbidden names: a new
    // field that leaks an account link or navigation data under any name fails here (assertion rule 4).
    private static readonly Dictionary<string, string[]> DocumentedFields = new()
    {
        ["/api/Doctor"] = ["id", "firstName", "lastName", "birthday", "address", "departmentId", "serviceIds"],
        ["/api/Patient"] = ["id", "userName", "phone", "email", "serviceId", "serviceName"],
        ["/api/Service"] = ["id", "name", "description", "departmentId"],
    };

    private readonly ApiFactory _factory;
    private readonly TestData _data;

    public CatalogTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
        _data = new TestData(_factory);
    }

    [Theory]
    [InlineData("/api/Doctor")]
    [InlineData("/api/Patient")]
    [InlineData("/api/Service")]
    [Trait("Category", "Security")]
    [Trait("Risk", "R-054")]
    [Trait("Scenario", "F-10.4")]
    public async Task Lists_DoNotExposeAccountLinksOrNavigationData(string path)
    {
        await _data.LinkedDoctorAsync();
        await _data.LinkedPatientAsync();
        using var client = _factory.ClientFor(TestUser.Nurse());

        var items = await client.GetFromJsonAsync<JsonElement>(path, TestData.Json);

        Assert.NotEmpty(items.EnumerateArray());
        Assert.All(items.EnumerateArray(), item =>
            Assert.Empty(item.EnumerateObject().Select(p => p.Name).Except(DocumentedFields[path])));
    }

    [Fact]
    [Trait("Category", "Security")]
    [Trait("Risk", "R-054")]
    [Trait("Scenario", "F-10.4")]
    public async Task Patient_ReadsOwnRecord_GetsNoAccountLink()
    {
        var (patient, patientId) = await _data.LinkedPatientAsync();
        using var client = _factory.ClientFor(patient);

        var item = await client.GetFromJsonAsync<JsonElement>($"/api/Patient/{patientId}", TestData.Json);

        Assert.Equal(patientId, item.GetProperty("id").GetInt32());
        Assert.Empty(item.EnumerateObject().Select(p => p.Name).Except(DocumentedFields["/api/Patient"]));
    }

    [Fact]
    [Trait("Category", "Security")]
    [Trait("Risk", "R-016")]
    public async Task CreateService_WithExtraFields_SetsOnlyDocumentedFields()
    {
        using var admin = _data.Admin;
        using var staff = _factory.ClientFor(TestUser.Nurse());
        // The injected rows point at records that exist, so only the API's binding can keep them out.
        var existingServiceId = await _data.ServiceIdAsync();
        var doctorId = await _data.CreateDoctorAsync();
        var injected = "Injected Patient " + Guid.NewGuid().ToString("N")[..6];
        var name = "Over-posting test " + Guid.NewGuid().ToString("N")[..6];

        var response = await admin.PostAsJsonAsync("/api/Service", new
        {
            id = 424242,
            name,
            patients = new[] { new { userName = injected, serviceId = existingServiceId } },
            doctorServices = new[] { new { doctorId, serviceId = existingServiceId } }
        }, TestData.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>(TestData.Json);
        var id = service.GetProperty("id").GetInt32();
        Assert.NotEqual(424242, id);
        Assert.Equal(name, (await admin.GetFromJsonAsync<JsonElement>($"/api/Service/{id}", TestData.Json))
            .GetProperty("name").GetString());
        var patients = (await staff.GetFromJsonAsync<JsonElement>("/api/Patient", TestData.Json)).EnumerateArray().ToList();
        Assert.NotEmpty(patients);
        Assert.DoesNotContain(patients, p => p.GetProperty("userName").GetString() == injected);
        var doctor = await admin.GetFromJsonAsync<JsonElement>($"/api/Doctor/{doctorId}", TestData.Json);
        Assert.Empty(doctor.GetProperty("serviceIds").EnumerateArray());
    }

    [Fact]
    [Trait("Risk", "R-057")]
    public async Task DeleteService_StillUsedByAPatient_Returns409_AndKeepsThePatient()
    {
        var serviceId = await _data.ServiceIdAsync();
        var patientId = await _data.CreatePatientAsync();
        using var admin = _data.Admin;

        var response = await admin.DeleteAsync($"/api/Service/{serviceId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/Patient/{patientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/Service/{serviceId}")).StatusCode);
    }

    [Fact]
    [Trait("Risk", "R-057")]
    public async Task DeleteService_Unused_Returns204()
    {
        using var admin = _data.Admin;
        var created = await admin.PostAsJsonAsync("/api/Service", new { name = "Unused " + Guid.NewGuid().ToString("N")[..6] }, TestData.Json);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestData.Json)).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/Service/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/Service/{id}")).StatusCode);
    }

    [Fact]
    [Trait("Risk", "R-019")]
    [Trait("Scenario", "F-12.3")]
    public async Task DeleteDoctor_WithAppointments_Returns409()
    {
        var (_, doctorId) = await _data.LinkedDoctorAsync();
        var patientId = await _data.CreatePatientAsync();
        await _data.CreateAppointmentAsync(patientId, doctorId, daysAhead: 12);
        using var admin = _data.Admin;

        var response = await admin.DeleteAsync($"/api/Doctor/{doctorId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/Doctor/{doctorId}")).StatusCode);
    }

    [Theory]
    [InlineData("serviceIds")]
    [InlineData("departmentId")]
    public async Task CreateDoctor_UnknownReference_Returns400(string field)
    {
        using var admin = _data.Admin;
        await _data.CreateDoctorAsync(); // the list below is not empty, so the absence can be seen
        var lastName = "Refused " + Guid.NewGuid().ToString("N")[..8];
        object body = field == "serviceIds"
            ? new { firstName = "Test", lastName, serviceIds = new[] { 987_654 } }
            : new { firstName = "Test", lastName, departmentId = 987_654 };

        var response = await admin.PostAsJsonAsync("/api/Doctor", body, TestData.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var doctors = (await admin.GetFromJsonAsync<JsonElement>("/api/Doctor", TestData.Json)).EnumerateArray().ToList();
        Assert.NotEmpty(doctors);
        Assert.DoesNotContain(doctors, d => d.GetProperty("lastName").GetString() == lastName);
    }

    [Fact]
    [Trait("Scenario", "F-02.2")]
    public async Task CreatePatient_UnknownService_Returns400()
    {
        using var admin = _data.Admin;
        await _data.CreatePatientAsync(); // the list below is not empty, so the absence can be seen
        var userName = "Refused Patient " + Guid.NewGuid().ToString("N")[..8];

        var response = await admin.PostAsJsonAsync("/api/Patient",
            new { userName, email = "p@hospital.test", serviceId = 987_654 }, TestData.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var patients = (await admin.GetFromJsonAsync<JsonElement>("/api/Patient", TestData.Json)).EnumerateArray().ToList();
        Assert.NotEmpty(patients);
        Assert.DoesNotContain(patients, p => p.GetProperty("userName").GetString() == userName);
    }

    [Fact]
    [Trait("Risk", "R-069")]
    [Trait("Scenario", "F-02.1")]
    public async Task UpdatePatient_ReturnsTheUpdatedPatient()
    {
        var patientId = await _data.CreatePatientAsync();
        using var admin = _data.Admin;

        var response = await admin.PutAsJsonAsync($"/api/Patient/{patientId}", new
        {
            userName = "Renamed Patient", phone = 20000000, email = "renamed@hospital.test",
            serviceId = await _data.ServiceIdAsync()
        }, TestData.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var patient = await response.Content.ReadFromJsonAsync<JsonElement>(TestData.Json);
        Assert.Equal(patientId, patient.GetProperty("id").GetInt32());
        Assert.Equal("Renamed Patient", patient.GetProperty("userName").GetString());
    }

    [Fact]
    [Trait("Category", "Security")]
    [Trait("Risk", "R-013")]
    [Trait("Risk", "R-042")]
    public async Task PatientChanges_AreAudited_WithoutPersonalData()
    {
        var patientId = await _data.CreatePatientAsync();
        using var admin = _data.Admin;
        var update = await admin.PutAsJsonAsync($"/api/Patient/{patientId}", new
        {
            userName = "Audit Check Patient", phone = 30000000, email = "auditcheck@hospital.test",
            serviceId = await _data.ServiceIdAsync()
        }, TestData.Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode); // otherwise the absence checks below prove nothing

        var entries = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/AuditLog?entityType=Patient&entityId={patientId}", TestData.Json);

        var actions = entries.EnumerateArray().Select(e => e.GetProperty("action").GetString()).ToList();
        Assert.Contains("Patient.Created", actions);
        Assert.Contains("Patient.Updated", actions);
        var raw = entries.GetRawText();
        Assert.DoesNotContain("Audit Check Patient", raw);
        Assert.DoesNotContain("auditcheck@hospital.test", raw);
        Assert.DoesNotContain("30000000", raw);
    }
}
