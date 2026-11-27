using System.Globalization;
using System.Net;
using System.Text.Json;
using HospitalManagement.E2E.Support;
using Reqnroll;

namespace HospitalManagement.E2E.Steps;

/// <summary>
/// The clinical journey through the deployed API. Every token is a real Keycloak token taken from a browser
/// sign-in (A-001), never a test token. Data is synthetic and created for this scenario (BUSINESS_FLOWS §4).
/// </summary>
[Binding]
public sealed class ClinicalJourneySteps : IDisposable
{
    private readonly BrowserSession _browser;
    private readonly Dictionary<string, HospitalApi> _apis = new();
    private HospitalApi? _current;

    private int _patientId, _doctorId, _otherPatientId, _otherRecordId, _recordId, _appointmentId;
    private HttpStatusCode _lastStatus;

    public ClinicalJourneySteps(BrowserSession browser) => _browser = browser;

    // ---------- Setup through the administrator (BUSINESS_FLOWS F-01, F-02) ----------

    [Given("the administrator has linked {string} to a new patient record")]
    public async Task LinkPatient(string user)
    {
        await SignedIn(user); // the account's profile exists after its first API call
        var admin = await ApiFor("admin.user");
        _patientId = await CreatePatientAsync(admin);
        await LinkAsync(admin, user, new { patientId = _patientId });
    }

    [Given("the administrator has linked {string} to a new doctor profile")]
    public async Task LinkDoctor(string user)
    {
        await SignedIn(user);
        var admin = await ApiFor("admin.user");
        var (status, doctor) = await admin.PostAsync("/api/Doctor",
            new { firstName = "E2E", lastName = "Doctor " + Tag(), serviceIds = Array.Empty<int>() });
        Assert.Equal(HttpStatusCode.Created, status);
        _doctorId = doctor.GetProperty("id").GetInt32();
        await LinkAsync(admin, user, new { doctorId = _doctorId });
    }

    [Given("another patient has a medical record written by that doctor")]
    public async Task OtherPatientRecord()
    {
        var admin = await ApiFor("admin.user");
        _otherPatientId = await CreatePatientAsync(admin);
        var doctor = await ApiFor("doctor.user");
        var (status, record) = await doctor.PostAsync("/api/MedicalRecord",
            new { patientId = _otherPatientId, diagnosis = "Another patient's diagnosis" });
        Assert.Equal(HttpStatusCode.Created, status);
        _otherRecordId = record.GetProperty("id").GetInt32();
    }

    // ---------- Who is acting ----------

    [Given("{string} is signed in")]
    public async Task SignedIn(string user) => _current = await ApiFor(user);

    // ---------- Booking (F-03, F-10) ----------

    [When("the patient books an appointment with the linked doctor")]
    public async Task Book()
    {
        var (status, appointment) = await _current!.PostAsync("/api/Appointment", new
        {
            patientId = _patientId, doctorId = _doctorId, scheduledAt = FutureSlot(), durationMinutes = 30,
            reason = "Synthetic check-up"
        });
        Assert.Equal(HttpStatusCode.Created, status);
        _appointmentId = appointment.GetProperty("id").GetInt32();
    }

    [Then("the appointment is in the patient's list with status {string}")]
    public async Task AppointmentListed(string expectedStatus)
    {
        var (_, list) = await _current!.GetAsync("/api/Appointment");
        var appointment = list.EnumerateArray().Single(a => a.GetProperty("id").GetInt32() == _appointmentId);
        Assert.Equal(expectedStatus, appointment.GetProperty("status").GetString());
    }

    [Then("every appointment in the patient's list belongs to the patient")]
    public async Task OnlyOwnAppointments()
    {
        var (status, list) = await _current!.GetAsync($"/api/Appointment?patientId={_otherPatientId}");
        Assert.Equal(HttpStatusCode.OK, status);
        // The other patient has no appointments, so an API that obeyed ?patientId= would return an empty list and
        // Assert.All alone would pass. The patient's own appointment must be there: the filter was overridden.
        Assert.Contains(list.EnumerateArray(), a => a.GetProperty("id").GetInt32() == _appointmentId);
        Assert.All(list.EnumerateArray(), a => Assert.Equal(_patientId, a.GetProperty("patientId").GetInt32()));
    }

    [When("the patient tries to book an appointment for the other patient")]
    public async Task BookForOther() =>
        (_lastStatus, _) = await _current!.PostAsync("/api/Appointment", new
        {
            patientId = _otherPatientId, doctorId = _doctorId, scheduledAt = FutureSlot(), durationMinutes = 30
        });

    [Then("the API refuses it with 403")]
    public void Refused() => Assert.Equal(HttpStatusCode.Forbidden, _lastStatus);

    // ---------- Medical records (F-08, F-09) ----------

    [When("the doctor records the diagnosis {string} for the linked patient")]
    [Given("the doctor has recorded the diagnosis {string} for the linked patient")]
    public async Task RecordDiagnosis(string diagnosis)
    {
        var (status, record) = await _current!.PostAsync("/api/MedicalRecord", new { patientId = _patientId, diagnosis });
        Assert.Equal(HttpStatusCode.Created, status);
        _recordId = record.GetProperty("id").GetInt32();
    }

    [Then("the record's author is the linked doctor profile")]
    public async Task AuthorIsLinkedDoctor()
    {
        var (_, record) = await _current!.GetAsync($"/api/MedicalRecord/{_recordId}");
        Assert.Equal(_doctorId, record.GetProperty("doctorId").GetInt32());
    }

    [Then("the patient sees the diagnosis {string} in their medical records")]
    public async Task PatientSeesDiagnosis(string diagnosis)
    {
        var (status, list) = await _current!.GetAsync("/api/MedicalRecord");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(list.EnumerateArray(), r => r.GetProperty("diagnosis").GetString() == diagnosis);
        Assert.All(list.EnumerateArray(), r => Assert.Equal(_patientId, r.GetProperty("patientId").GetInt32()));
    }

    [Then("opening the other patient's medical record is refused with 403")]
    public async Task OtherRecordRefused() =>
        Assert.Equal(HttpStatusCode.Forbidden, (await _current!.GetAsync($"/api/MedicalRecord/{_otherRecordId}")).Status);

    [Then("opening a medical record that does not exist is refused with 403")]
    public async Task MissingRecordRefused() =>
        Assert.Equal(HttpStatusCode.Forbidden, (await _current!.GetAsync("/api/MedicalRecord/999999999")).Status);

    [When("the doctor amends the diagnosis to {string}")]
    public async Task Amend(string diagnosis)
    {
        var (status, _) = await _current!.PutAsync($"/api/MedicalRecord/{_recordId}", new { diagnosis });
        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Then("the record's history shows {string} then {string}")]
    public async Task HistoryShows(string first, string second)
    {
        var (status, history) = await _current!.GetAsync($"/api/MedicalRecord/{_recordId}/history");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(new[] { first, second },
            history.EnumerateArray().Select(r => r.GetProperty("diagnosis").GetString()).ToArray());
    }

    [Then("reading the record's history is refused with 403")]
    public async Task HistoryRefused() =>
        Assert.Equal(HttpStatusCode.Forbidden, (await _current!.GetAsync($"/api/MedicalRecord/{_recordId}/history")).Status);

    // ---------- Helpers ----------

    /// <summary>Signs the user in through the real login page once per scenario and keeps an API client.</summary>
    private async Task<HospitalApi> ApiFor(string user)
    {
        if (_apis.TryGetValue(user, out var api)) return api;
        await _browser.Page.Context.ClearCookiesAsync(); // a fresh Keycloak session for each user
        await _browser.SignInFromStartAsync(user, E2EConfig.UsersPassword);
        api = new HospitalApi(await _browser.CaptureAccessTokenAsync());
        await api.GetAsync("/api/User/me"); // the first API call provisions the local profile
        _apis[user] = api;
        return api;
    }

    private static async Task<int> CreatePatientAsync(HospitalApi admin)
    {
        var (_, services) = await admin.GetAsync("/api/Service");
        int serviceId;
        if (services.ValueKind == JsonValueKind.Array && services.GetArrayLength() > 0)
            serviceId = services[0].GetProperty("id").GetInt32();
        else
        {
            var (_, created) = await admin.PostAsync("/api/Service", new { name = "E2E service " + Tag() });
            serviceId = created.GetProperty("id").GetInt32();
        }

        var tag = Tag();
        var (status, patient) = await admin.PostAsync("/api/Patient", new
        {
            userName = $"E2E Patient {tag}", phone = 10000000, email = $"e2e.{tag}@hospital.test", serviceId
        });
        Assert.Equal(HttpStatusCode.Created, status);
        return patient.GetProperty("id").GetInt32();
    }

    private static async Task LinkAsync(HospitalApi admin, string user, object link)
    {
        var (_, users) = await admin.GetAsync("/api/User");
        var userId = users.EnumerateArray().Single(u => u.GetProperty("userName").GetString() == user).GetProperty("id").GetInt32();
        var (status, _) = await admin.PutAsync($"/api/User/{userId}/link", link);
        Assert.Equal(HttpStatusCode.OK, status);
    }

    /// <summary>A free slot: the doctor is new for every scenario, so any future time works.</summary>
    private static string FutureSlot() =>
        DateTime.UtcNow.Date.AddDays(14).AddHours(9).AddMinutes(Random.Shared.Next(0, 40) * 15.0).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    public void Dispose()
    {
        foreach (var api in _apis.Values) api.Dispose();
    }
}
