using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagement.ApiTests.Infrastructure;

namespace HospitalManagement.ApiTests.Security;

/// <summary>Record author and editor, SECURITY_TESTING §3 "Record author and editor"; BUSINESS_FLOWS F-08, F-09.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public class RecordAuthorTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly TestData _data;

    private TestUser _d1 = null!, _d2 = null!;
    private int _d1Id, _d2Id, _patientId;

    public RecordAuthorTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
        _data = new TestData(_factory);
    }

    public async Task InitializeAsync()
    {
        (_d1, _d1Id) = await _data.LinkedDoctorAsync();
        (_d2, _d2Id) = await _data.LinkedDoctorAsync();
        _patientId = await _data.CreatePatientAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Risk", "R-010")]
    [Trait("Scenario", "F-08.2")]
    public async Task CreateRecord_BodyNamesAnotherDoctor_AuthorIsTheCaller()
    {
        using var client = _factory.ClientFor(_d1);

        var response = await client.PostAsJsonAsync("/api/MedicalRecord",
            new { patientId = _patientId, doctorId = _d2Id, diagnosis = "Synthetic diagnosis" }, TestData.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var record = await response.Content.ReadFromJsonAsync<JsonElement>(TestData.Json);
        Assert.Equal(_d1Id, record.GetProperty("doctorId").GetInt32());
    }

    [Fact]
    [Trait("Risk", "R-010")]
    [Trait("Scenario", "F-08.3")]
    public async Task CreateRecord_ForAnotherDoctorsAppointment_Returns400_AndCreatesNothing()
    {
        var appointmentOfD2 = await _data.CreateAppointmentAsync(_patientId, _d2Id, daysAhead: 9);
        using var client = _factory.ClientFor(_d1);
        using var nurse = _factory.ClientFor(TestUser.Nurse());
        var before = await CountRecordsAsync(nurse);

        var response = await client.PostAsJsonAsync("/api/MedicalRecord",
            new { patientId = _patientId, appointmentId = appointmentOfD2, diagnosis = "Synthetic" }, TestData.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await CountRecordsAsync(nurse));
    }

    [Fact]
    [Trait("Risk", "R-053")]
    [Trait("Scenario", "F-08.4")]
    public async Task CreateRecord_ForOwnAppointmentThatHasNotStarted_Returns409_AndCreatesNothing()
    {
        var futureAppointment = await _data.CreateAppointmentAsync(_patientId, _d1Id, daysAhead: 10);
        using var client = _factory.ClientFor(_d1);
        using var nurse = _factory.ClientFor(TestUser.Nurse());
        var before = await CountRecordsAsync(nurse);

        var response = await client.PostAsJsonAsync("/api/MedicalRecord",
            new { patientId = _patientId, appointmentId = futureAppointment, diagnosis = "Synthetic" }, TestData.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(before, await CountRecordsAsync(nurse));
    }

    [Fact]
    [Trait("Risk", "R-021")]
    [Trait("Scenario", "F-01.2")]
    public async Task UnlinkedDoctor_CreatesRecord_Returns403()
    {
        using var client = _factory.ClientFor(TestUser.Doctor());
        using var nurse = _factory.ClientFor(TestUser.Nurse());
        var before = await CountRecordsAsync(nurse);

        var response = await client.PostAsJsonAsync("/api/MedicalRecord",
            new { patientId = _patientId, diagnosis = "Synthetic" }, TestData.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, await CountRecordsAsync(nurse));
    }

    [Fact]
    [Trait("Risk", "R-011")]
    [Trait("Scenario", "F-09.1")]
    public async Task AnotherDoctorAmendsRecord_EditorAndRevisionAreRecorded()
    {
        var recordId = await _data.CreateRecordAsync(_d1, _patientId, "Original diagnosis");
        using var d2 = _factory.ClientFor(_d2);

        var response = await d2.PutAsJsonAsync($"/api/MedicalRecord/{recordId}",
            new { diagnosis = "Amended diagnosis", treatment = "Rest" }, TestData.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = await response.Content.ReadFromJsonAsync<JsonElement>(TestData.Json);
        Assert.Equal(_d1Id, record.GetProperty("doctorId").GetInt32());
        Assert.Equal(_d2Id, record.GetProperty("lastEditedByDoctorId").GetInt32());
        Assert.Equal(2, record.GetProperty("revision").GetInt32());

        var history = await d2.GetFromJsonAsync<JsonElement>($"/api/MedicalRecord/{recordId}/history", TestData.Json);
        var revisions = history.EnumerateArray().ToList();
        Assert.Equal(2, revisions.Count);
        Assert.Equal("Original diagnosis", revisions[0].GetProperty("diagnosis").GetString());
        Assert.Equal(_d1Id, revisions[0].GetProperty("editedByDoctorId").GetInt32());
        Assert.Equal("Amended diagnosis", revisions[1].GetProperty("diagnosis").GetString());
        Assert.Equal(_d2Id, revisions[1].GetProperty("editedByDoctorId").GetInt32());
        Assert.Equal(new[] { "Diagnosis", "Treatment" },
            revisions[1].GetProperty("changedFields").EnumerateArray().Select(f => f.GetString()).ToArray());
    }

    [Fact]
    [Trait("Risk", "R-011")]
    [Trait("Scenario", "F-09.2")]
    public async Task EditWithoutChanges_CreatesNoRevision()
    {
        var recordId = await _data.CreateRecordAsync(_d1, _patientId, "Unchanged diagnosis");
        using var d1 = _factory.ClientFor(_d1);

        var response = await d1.PutAsJsonAsync($"/api/MedicalRecord/{recordId}",
            new { diagnosis = "Unchanged diagnosis" }, TestData.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await d1.GetFromJsonAsync<JsonElement>($"/api/MedicalRecord/{recordId}/history", TestData.Json);
        Assert.Equal(1, history.GetArrayLength());
    }

    [Theory]
    [InlineData("Patient")]
    [InlineData("Admin")]
    [Trait("Risk", "R-020")]
    [Trait("Scenario", "F-09.5")]
    public async Task PatientOrAdmin_ReadsHistory_Returns403(string role)
    {
        var recordId = await _data.CreateRecordAsync(_d1, _patientId);
        using var client = _factory.ClientFor(role == "Patient" ? TestUser.Patient() : TestUser.Admin());

        var response = await client.GetAsync($"/api/MedicalRecord/{recordId}/history");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>The records of this test's own patient only, so another test's records cannot change the count.</summary>
    private async Task<int> CountRecordsAsync(HttpClient clinicalStaff) =>
        (await clinicalStaff.GetFromJsonAsync<JsonElement>($"/api/MedicalRecord?patientId={_patientId}", TestData.Json))
        .GetArrayLength();
}
