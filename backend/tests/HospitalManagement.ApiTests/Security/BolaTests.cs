using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagement.ApiTests.Infrastructure;

namespace HospitalManagement.ApiTests.Security;

/// <summary>
/// Object-level access, docs/testing/SECURITY_TESTING.md §3 and BUSINESS_FLOWS F-10: a patient reaches only
/// the patient record linked to their account; "missing" and "not yours" look the same.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public class BolaTests : IAsyncLifetime
{
    private const int MissingId = 999_999;
    private readonly ApiFactory _factory;
    private readonly TestData _data;

    private TestUser _patientA = null!, _patientB = null!;
    private int _patientAId, _patientBId, _doctorId, _appointmentA, _appointmentB, _recordA, _recordB;

    public BolaTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
        _data = new TestData(_factory);
    }

    public async Task InitializeAsync()
    {
        (_patientA, _patientAId) = await _data.LinkedPatientAsync();
        (_patientB, _patientBId) = await _data.LinkedPatientAsync();
        var (doctor, doctorId) = await _data.LinkedDoctorAsync();
        _doctorId = doctorId;
        _appointmentA = await _data.CreateAppointmentAsync(_patientAId, doctorId, daysAhead: 7);
        _appointmentB = await _data.CreateAppointmentAsync(_patientBId, doctorId, daysAhead: 8);
        _recordA = await _data.CreateRecordAsync(doctor, _patientAId);
        _recordB = await _data.CreateRecordAsync(doctor, _patientBId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("/api/Patient/{0}")]
    [InlineData("/api/Appointment/{0}")]
    [InlineData("/api/MedicalRecord/{0}")]
    [Trait("Suite", "Smoke")]
    [Trait("Risk", "R-001")]
    [Trait("Risk", "R-002")]
    [Trait("Risk", "R-003")]
    [Trait("Scenario", "F-10.1")]
    public async Task Patient_ReadsOwnItem_Returns200(string pathFormat)
    {
        using var client = _factory.ClientFor(_patientA);

        var response = await client.GetAsync(string.Format(CultureInfo.InvariantCulture, pathFormat, OwnId(pathFormat)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // 200 alone would also pass if the API answered with another patient's data: the body must be the item asked for.
        var item = await response.Content.ReadFromJsonAsync<JsonElement>(TestData.Json);
        Assert.Equal(OwnId(pathFormat), item.GetProperty("id").GetInt32());
        var owner = pathFormat.Contains("Patient/") ? item.GetProperty("id") : item.GetProperty("patientId");
        Assert.Equal(_patientAId, owner.GetInt32());
    }

    [Theory]
    [InlineData("/api/Patient/{0}")]
    [InlineData("/api/Appointment/{0}")]
    [InlineData("/api/MedicalRecord/{0}")]
    [Trait("Risk", "R-001")]
    [Trait("Risk", "R-002")]
    [Trait("Risk", "R-003")]
    [Trait("Scenario", "F-10.2")]
    public async Task Patient_ReadsOtherPatientsItem_Returns403(string pathFormat)
    {
        using var client = _factory.ClientFor(_patientA);

        var response = await client.GetAsync(string.Format(CultureInfo.InvariantCulture, pathFormat, OtherId(pathFormat)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync()); // nothing of patient B leaks
    }

    [Theory]
    [InlineData("/api/Patient/{0}")]
    [InlineData("/api/Appointment/{0}")]
    [InlineData("/api/MedicalRecord/{0}")]
    [Trait("Risk", "R-004")]
    [Trait("Scenario", "F-10.2")]
    public async Task Patient_ReadsMissingId_Returns403LikeOtherPatientsId(string pathFormat)
    {
        using var client = _factory.ClientFor(_patientA);

        var response = await client.GetAsync(string.Format(CultureInfo.InvariantCulture, pathFormat, MissingId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync()); // like another patient's id: nothing tells them apart
    }

    [Theory]
    [InlineData("/api/Appointment/{0}")]
    [InlineData("/api/MedicalRecord/{0}")]
    [Trait("Risk", "R-004")]
    public async Task Staff_ReadsMissingId_Returns404(string pathFormat)
    {
        using var client = _factory.ClientFor(TestUser.Nurse());

        var response = await client.GetAsync(string.Format(CultureInfo.InvariantCulture, pathFormat, MissingId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/Appointment")]
    [InlineData("/api/MedicalRecord")]
    [Trait("Risk", "R-001")]
    [Trait("Risk", "R-002")]
    [Trait("Scenario", "F-10.1")]
    public async Task Patient_ListsItems_SeesOnlyOwn_EvenWhenAskingForAnotherPatient(string path)
    {
        using var client = _factory.ClientFor(_patientA);

        var items = await client.GetFromJsonAsync<JsonElement>($"{path}?patientId={_patientBId}", TestData.Json);

        Assert.NotEmpty(items.EnumerateArray());
        Assert.All(items.EnumerateArray(), i => Assert.Equal(_patientAId, i.GetProperty("patientId").GetInt32()));
    }

    [Theory]
    [InlineData("/api/Appointment")]
    [InlineData("/api/MedicalRecord")]
    [Trait("Risk", "R-001")]
    [Trait("Scenario", "F-10.3")]
    public async Task UnlinkedPatient_ListsItems_GetsEmptyList(string path)
    {
        using var client = _factory.ClientFor(TestUser.Patient());

        var items = await client.GetFromJsonAsync<JsonElement>(path, TestData.Json);

        Assert.Empty(items.EnumerateArray());
    }

    // A patient account not linked to any patient record owns nothing: asking for a real patient's item by id must be
    // refused exactly like asking for someone else's item, with nothing in the body (F-10.3, SECURITY_TESTING §3).
    [Theory]
    [InlineData("/api/Patient/{0}")]
    [InlineData("/api/Appointment/{0}")]
    [InlineData("/api/MedicalRecord/{0}")]
    [Trait("Risk", "R-001")]
    [Trait("Risk", "R-002")]
    [Trait("Risk", "R-003")]
    [Trait("Scenario", "F-10.3")]
    public async Task UnlinkedPatient_ReadsAPatientsItem_Returns403_AndNothingLeaks(string pathFormat)
    {
        using var client = _factory.ClientFor(TestUser.Patient());

        var response = await client.GetAsync(string.Format(CultureInfo.InvariantCulture, pathFormat, OwnId(pathFormat)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Risk", "R-002")]
    [Trait("Scenario", "F-03.8")]
    public async Task Patient_BooksForAnotherPatient_Returns403_AndCreatesNothing()
    {
        using var client = _factory.ClientFor(_patientA);
        using var staff = _factory.ClientFor(TestUser.Nurse());
        var before = await CountAppointmentsAsync(staff, _patientBId);

        var response = await client.PostAsJsonAsync("/api/Appointment", new
        {
            // A real doctor and a free slot: without the ownership check this booking would succeed.
            patientId = _patientBId, doctorId = _doctorId, scheduledAt = DateTime.UtcNow.Date.AddDays(20).AddHours(9),
            durationMinutes = 30
        }, TestData.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, await CountAppointmentsAsync(staff, _patientBId));
    }

    [Fact]
    [Trait("Risk", "R-002")]
    [Trait("Scenario", "F-06.4")]
    public async Task Patient_CancelsAnotherPatientsAppointment_Returns403_AndLeavesItUnchanged()
    {
        using var client = _factory.ClientFor(_patientA);
        using var staff = _factory.ClientFor(TestUser.Nurse());

        var response = await client.PostAsync($"/api/Appointment/{_appointmentB}/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var appointment = await staff.GetFromJsonAsync<JsonElement>($"/api/Appointment/{_appointmentB}", TestData.Json);
        Assert.Equal("Scheduled", appointment.GetProperty("status").GetString());
    }

    private int OwnId(string pathFormat) => pathFormat.Contains("Patient/") ? _patientAId
        : pathFormat.Contains("Appointment") ? _appointmentA : _recordA;

    private int OtherId(string pathFormat) => pathFormat.Contains("Patient/") ? _patientBId
        : pathFormat.Contains("Appointment") ? _appointmentB : _recordB;

    private static async Task<int> CountAppointmentsAsync(HttpClient staff, int patientId) =>
        (await staff.GetFromJsonAsync<JsonElement>($"/api/Appointment?patientId={patientId}", TestData.Json))
        .GetArrayLength();
}
