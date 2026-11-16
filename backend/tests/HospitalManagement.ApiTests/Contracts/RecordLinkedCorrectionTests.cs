using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagement.ApiTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Services.AppointmentService;
using Services.MedicalRecordService;

namespace HospitalManagement.ApiTests.Contracts;

/// <summary>
/// R-053 end to end: a visit that was documented with a medical record cannot later be corrected to NoShow
/// (BUSINESS_FLOWS F-11.6). Appointments can only be booked in the future, so a second host runs the business
/// rules eight days ahead: there the appointment has started and can be completed and documented.
/// Only the business services see the shifted clock; token validation and the audit keep the real time.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Contracts")]
public class RecordLinkedCorrectionTests : IAsyncLifetime
{
    private static readonly TimeSpan EightDaysLater = TimeSpan.FromDays(8);

    private readonly ApiFixture _fixture;
    private readonly TestData _data;
    private ApiFactory _later = null!;

    public RecordLinkedCorrectionTests(ApiFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture.Factory); // the normal host books the appointment
    }

    // xUnit creates the class once per test, so each test builds its own host; keep the number of tests small.
    public Task InitializeAsync()
    {
        _later = new ApiFactory(_fixture.ConnectionString, services =>
        {
            var clock = new ShiftedClock(EightDaysLater);
            services.RemoveAll<IAppointmentService>();
            services.AddScoped<IAppointmentService>(sp =>
                ActivatorUtilities.CreateInstance<AppointmentService>(sp, (TimeProvider)clock));
            services.RemoveAll<IMedicalRecordService>();
            services.AddScoped<IMedicalRecordService>(sp =>
                ActivatorUtilities.CreateInstance<MedicalRecordService>(sp, (TimeProvider)clock));
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _later.DisposeAsync();

    [Fact]
    [Trait("Risk", "R-053")]
    [Trait("Risk", "R-022")]
    [Trait("Scenario", "F-11.6")]
    public async Task CorrectStatus_DocumentedVisitToNoShow_Returns409_AndStaysCompleted()
    {
        var (doctor, appointmentId) = await DocumentedVisitAsync();
        using var doctorClient = _later.ClientFor(doctor);
        Assert.Equal(HttpStatusCode.OK, (await PutStatusAsync(doctorClient, appointmentId, "Completed")).StatusCode);

        using var admin = _later.ClientFor(TestUser.Admin());
        var response = await admin.PostAsJsonAsync($"/api/Appointment/{appointmentId}/correct-status",
            new { status = "NoShow", reason = "Marked on the wrong appointment" }, TestData.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var after = await admin.GetFromJsonAsync<JsonElement>($"/api/Appointment/{appointmentId}", TestData.Json);
        Assert.Equal("Completed", after.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Risk", "R-053")]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-07.5")]
    public async Task Put_DocumentedVisitToNoShow_Returns409_AndStaysScheduled()
    {
        var (_, appointmentId) = await DocumentedVisitAsync();
        using var nurse = _later.ClientFor(TestUser.Nurse());

        var response = await PutStatusAsync(nurse, appointmentId, "NoShow");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var after = await nurse.GetFromJsonAsync<JsonElement>($"/api/Appointment/{appointmentId}", TestData.Json);
        Assert.Equal("Scheduled", after.GetProperty("status").GetString());
    }

    /// <summary>
    /// Booked a week ahead on the real clock; eight days later the visit has started (still Scheduled)
    /// and the doctor has written a record for it.
    /// </summary>
    private async Task<(TestUser Doctor, int AppointmentId)> DocumentedVisitAsync()
    {
        var (doctor, doctorId) = await _data.LinkedDoctorAsync();
        var patientId = await _data.CreatePatientAsync();
        var appointmentId = await _data.CreateAppointmentAsync(patientId, doctorId, daysAhead: 7);
        using var client = _later.ClientFor(doctor);
        var record = await client.PostAsJsonAsync("/api/MedicalRecord",
            new { patientId, appointmentId, diagnosis = "Synthetic diagnosis" }, TestData.Json);
        Assert.Equal(HttpStatusCode.Created, record.StatusCode);
        return (doctor, appointmentId);
    }

    /// <summary>PUT with every field as it is now, only the status changed (so nothing else is cleared).</summary>
    private static async Task<HttpResponseMessage> PutStatusAsync(HttpClient client, int appointmentId, string status)
    {
        var a = await client.GetFromJsonAsync<JsonElement>($"/api/Appointment/{appointmentId}", TestData.Json);
        return await client.PutAsJsonAsync($"/api/Appointment/{appointmentId}", new
        {
            doctorId = a.GetProperty("doctorId").GetInt32(),
            serviceId = a.GetProperty("serviceId").ValueKind == JsonValueKind.Number ? a.GetProperty("serviceId").GetInt32() : (int?)null,
            scheduledAt = a.GetProperty("scheduledAt").GetDateTime(),
            durationMinutes = a.GetProperty("durationMinutes").GetInt32(),
            reason = a.GetProperty("reason").GetString(),
            notes = a.GetProperty("notes").GetString(),
            status
        }, TestData.Json);
    }

    /// <summary>The real clock moved by a fixed offset.</summary>
    private sealed class ShiftedClock(TimeSpan offset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + offset;
    }
}
