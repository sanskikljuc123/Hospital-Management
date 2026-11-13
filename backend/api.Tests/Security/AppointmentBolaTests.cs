using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using api.Tests.Infrastructure;
using Domain.Dtos;
using Domain.Entities;

namespace api.Tests.Security;

/// <summary>
/// Object-level authorization for appointments (docs/qe-framework/SECURITY_TESTING.md section 8).
/// Ownership rule, from docs/API_ENDPOINTS.md: a Patient may list, read, book and cancel only the
/// appointments of the patient record linked to their account; staff may act on any.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Gate", "G-SEC-02")]
public class AppointmentBolaTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _api;
    private Patient _recordA = null!;
    private Patient _recordB = null!;
    private Appointment _appointmentA = null!;
    private Appointment _appointmentB = null!;

    public AppointmentBolaTests(ApiFactory api) => _api = api;

    public async Task InitializeAsync()
    {
        _recordA = await _api.CreatePatientAsync();
        _recordB = await _api.CreatePatientAsync();
        await _api.LinkToPatientAsync(TestIdentities.PatientA, _recordA.Id);
        await _api.LinkToPatientAsync(TestIdentities.PatientB, _recordB.Id);
        _appointmentA = await _api.CreateAppointmentAsync(_recordA.Id);
        _appointmentB = await _api.CreateAppointmentAsync(_recordB.Id);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task AssertRefusedWithoutDataAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    private async Task<List<AppointmentDto>> ListAsync(string url, TestIdentity identity)
    {
        var response = await _api.SendAsync(HttpMethod.Get, url, identity);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<AppointmentDto>>(Json))!;
    }

    [Fact, Trait("Case", "BOLA-01")]
    public async Task Patient_reads_their_own_appointment()
    {
        var response = await _api.SendAsync(HttpMethod.Get, $"/api/Appointment/{_appointmentA.Id}", TestIdentities.PatientA);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AppointmentDto>(Json);
        Assert.Equal(_appointmentA.Id, body!.Id);
        Assert.Equal(_recordA.Id, body.PatientId);
    }

    [Fact, Trait("Case", "BOLA-05")]
    public async Task Patient_cannot_read_another_patients_appointment()
    {
        var response = await _api.SendAsync(HttpMethod.Get, $"/api/Appointment/{_appointmentB.Id}", TestIdentities.PatientA);

        await AssertRefusedWithoutDataAsync(response);
    }

    [Fact, Trait("Case", "BOLA-04")]
    public async Task Patient_lists_only_their_own_appointments()
    {
        var appointments = await ListAsync("/api/Appointment", TestIdentities.PatientA);

        Assert.Contains(appointments, a => a.Id == _appointmentA.Id);
        Assert.All(appointments, a => Assert.Equal(_recordA.Id, a.PatientId));
    }

    [Fact, Trait("Case", "BOLA-04")]
    public async Task Patient_cannot_widen_the_list_with_another_patients_id()
    {
        var appointments = await ListAsync($"/api/Appointment?patientId={_recordB.Id}", TestIdentities.PatientA);

        Assert.DoesNotContain(appointments, a => a.Id == _appointmentB.Id);
        Assert.All(appointments, a => Assert.Equal(_recordA.Id, a.PatientId));
    }

    [Fact, Trait("Case", "BOLA-04")]
    public async Task Patient_cannot_reach_other_appointments_through_the_doctor_filter()
    {
        var appointments = await ListAsync($"/api/Appointment?doctorId={_appointmentB.DoctorId}", TestIdentities.PatientA);

        Assert.Empty(appointments);
    }

    [Fact, Trait("Case", "BOLA-03")]
    public async Task Unlinked_patient_sees_an_empty_list_and_no_single_appointment()
    {
        var appointments = await ListAsync("/api/Appointment", TestIdentities.PatientUnlinked);
        var single = await _api.SendAsync(HttpMethod.Get, $"/api/Appointment/{_appointmentA.Id}", TestIdentities.PatientUnlinked);

        Assert.Empty(appointments);
        await AssertRefusedWithoutDataAsync(single);
    }

    [Fact, Trait("Case", "BOLA-01")]
    public async Task Patient_books_an_appointment_for_themselves()
    {
        var response = await _api.SendAsync(HttpMethod.Post, "/api/Appointment", TestIdentities.PatientA,
            new AppointmentCreateDto
            {
                PatientId = _recordA.Id,
                DoctorId = await _api.CreateDoctorAsync(),
                ScheduledAt = DateTime.UtcNow.AddDays(40),
                DurationMinutes = 30
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AppointmentDto>(Json);
        Assert.Equal(_recordA.Id, (await _api.ReadAppointmentAsync(created!.Id))!.PatientId);
    }

    [Fact, Trait("Case", "BOLA-07")]
    public async Task Patient_cannot_book_an_appointment_for_another_patient()
    {
        var doctorId = await _api.CreateDoctorAsync();

        var response = await _api.SendAsync(HttpMethod.Post, "/api/Appointment", TestIdentities.PatientA,
            new AppointmentCreateDto
            {
                PatientId = _recordB.Id,
                DoctorId = doctorId,
                ScheduledAt = DateTime.UtcNow.AddDays(40),
                DurationMinutes = 30
            });

        await AssertRefusedWithoutDataAsync(response);
        Assert.False(await _api.WithDbAsync(db => Task.FromResult(db.Appointments.Any(a => a.DoctorId == doctorId))));
    }

    [Fact, Trait("Case", "BOLA-01")]
    public async Task Patient_cancels_their_own_appointment()
    {
        var response = await _api.SendAsync(HttpMethod.Post, $"/api/Appointment/{_appointmentA.Id}/cancel", TestIdentities.PatientA);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AppointmentStatus.Cancelled, (await _api.ReadAppointmentAsync(_appointmentA.Id))!.Status);
    }

    [Fact, Trait("Case", "BOLA-06")]
    public async Task Patient_cannot_cancel_another_patients_appointment()
    {
        var response = await _api.SendAsync(HttpMethod.Post, $"/api/Appointment/{_appointmentB.Id}/cancel", TestIdentities.PatientA);

        await AssertRefusedWithoutDataAsync(response);
        Assert.Equal(AppointmentStatus.Scheduled, (await _api.ReadAppointmentAsync(_appointmentB.Id))!.Status);
    }

    [Fact, Trait("Case", "BOLA-06")]
    public async Task Patient_cannot_reschedule_even_their_own_appointment()
    {
        // Updating is documented as staff-only.
        var response = await _api.SendAsync(HttpMethod.Put, $"/api/Appointment/{_appointmentA.Id}", TestIdentities.PatientA,
            new AppointmentUpdateDto
            {
                DoctorId = _appointmentA.DoctorId,
                ScheduledAt = DateTime.UtcNow.AddDays(50),
                DurationMinutes = 60,
                Status = AppointmentStatus.Confirmed
            });

        await AssertRefusedWithoutDataAsync(response);
        var stored = await _api.ReadAppointmentAsync(_appointmentA.Id);
        Assert.Equal(30, stored!.DurationMinutes);
        Assert.Equal(AppointmentStatus.Scheduled, stored.Status);
    }

    [Fact, Trait("Case", "BOLA-06")]
    public async Task Patient_cannot_delete_another_patients_appointment()
    {
        var response = await _api.SendAsync(HttpMethod.Delete, $"/api/Appointment/{_appointmentB.Id}", TestIdentities.PatientA);

        await AssertRefusedWithoutDataAsync(response);
        Assert.NotNull(await _api.ReadAppointmentAsync(_appointmentB.Id));
    }

    [Fact, Trait("Case", "BOLA-08")]
    public async Task Patient_cannot_tell_a_missing_appointment_from_another_patients_appointment()
    {
        const int missingId = int.MaxValue;
        Assert.Null(await _api.ReadAppointmentAsync(missingId));

        var missing = await _api.SendAsync(HttpMethod.Get, $"/api/Appointment/{missingId}", TestIdentities.PatientA);
        var others = await _api.SendAsync(HttpMethod.Get, $"/api/Appointment/{_appointmentB.Id}", TestIdentities.PatientA);

        Assert.Equal(others.StatusCode, missing.StatusCode);
        Assert.Equal(await others.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
    }

    [Fact, Trait("Case", "BOLA-08")]
    public async Task Patient_cannot_probe_for_appointments_through_cancel()
    {
        const int missingId = int.MaxValue;

        var missing = await _api.SendAsync(HttpMethod.Post, $"/api/Appointment/{missingId}/cancel", TestIdentities.PatientA);
        var others = await _api.SendAsync(HttpMethod.Post, $"/api/Appointment/{_appointmentB.Id}/cancel", TestIdentities.PatientA);

        Assert.Equal(others.StatusCode, missing.StatusCode);
        Assert.Equal(await others.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
    }
}
