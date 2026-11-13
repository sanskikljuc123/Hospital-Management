using System.Net;
using System.Net.Http.Json;
using api.Tests.Infrastructure;
using Domain.Dtos;
using Domain.Entities;

namespace api.Tests.Security;

/// <summary>
/// Object-level authorization for medical records (docs/qe-framework/SECURITY_TESTING.md section 8).
/// Ownership rule, from docs/API_ENDPOINTS.md: a Patient may list and read only the records of the
/// patient record linked to their account; doctors and nurses read all; admins have no access to
/// clinical content; only doctors write.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Gate", "G-SEC-02")]
public class MedicalRecordBolaTests : IAsyncLifetime
{
    private readonly ApiFactory _api;
    private Patient _patientA = null!;
    private Patient _patientB = null!;
    private MedicalRecord _recordOfA = null!;
    private MedicalRecord _recordOfB = null!;

    public MedicalRecordBolaTests(ApiFactory api) => _api = api;

    public async Task InitializeAsync()
    {
        _patientA = await _api.CreatePatientAsync();
        _patientB = await _api.CreatePatientAsync();
        await _api.LinkToPatientAsync(TestIdentities.PatientA, _patientA.Id);
        await _api.LinkToPatientAsync(TestIdentities.PatientB, _patientB.Id);
        _recordOfA = await _api.CreateMedicalRecordAsync(_patientA.Id);
        _recordOfB = await _api.CreateMedicalRecordAsync(_patientB.Id);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task AssertRefusedWithoutDataAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    private async Task<List<MedicalRecordDto>> ListAsync(string url, TestIdentity identity)
    {
        var response = await _api.SendAsync(HttpMethod.Get, url, identity);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<MedicalRecordDto>>())!;
    }

    [Fact, Trait("Case", "BOLA-01")]
    public async Task Patient_reads_their_own_medical_record()
    {
        var response = await _api.SendAsync(HttpMethod.Get, $"/api/MedicalRecord/{_recordOfA.Id}", TestIdentities.PatientA);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MedicalRecordDto>();
        Assert.Equal(_recordOfA.Diagnosis, body!.Diagnosis);
    }

    [Fact, Trait("Case", "BOLA-05")]
    public async Task Patient_cannot_read_another_patients_medical_record()
    {
        var response = await _api.SendAsync(HttpMethod.Get, $"/api/MedicalRecord/{_recordOfB.Id}", TestIdentities.PatientA);

        await AssertRefusedWithoutDataAsync(response);
    }

    [Fact, Trait("Case", "BOLA-04")]
    public async Task Patient_lists_only_their_own_medical_records()
    {
        var records = await ListAsync("/api/MedicalRecord", TestIdentities.PatientA);

        Assert.Contains(records, r => r.Id == _recordOfA.Id);
        Assert.All(records, r => Assert.Equal(_patientA.Id, r.PatientId));
    }

    [Fact, Trait("Case", "BOLA-04")]
    public async Task Patient_cannot_widen_the_list_with_another_patients_id()
    {
        var records = await ListAsync($"/api/MedicalRecord?patientId={_patientB.Id}", TestIdentities.PatientA);

        Assert.DoesNotContain(records, r => r.Id == _recordOfB.Id);
        Assert.All(records, r => Assert.Equal(_patientA.Id, r.PatientId));
    }

    [Fact, Trait("Case", "BOLA-03")]
    public async Task Unlinked_patient_sees_an_empty_list_and_no_single_record()
    {
        var records = await ListAsync("/api/MedicalRecord", TestIdentities.PatientUnlinked);
        var single = await _api.SendAsync(HttpMethod.Get, $"/api/MedicalRecord/{_recordOfA.Id}", TestIdentities.PatientUnlinked);

        Assert.Empty(records);
        await AssertRefusedWithoutDataAsync(single);
    }

    [Fact, Trait("Case", "BOLA-06")]
    public async Task Patient_cannot_change_even_their_own_medical_record()
    {
        var response = await _api.SendAsync(HttpMethod.Put, $"/api/MedicalRecord/{_recordOfA.Id}", TestIdentities.PatientA,
            new MedicalRecordUpdateDto { Diagnosis = "rewritten-by-patient" });

        await AssertRefusedWithoutDataAsync(response);
        Assert.Equal(_recordOfA.Diagnosis, (await _api.ReadMedicalRecordAsync(_recordOfA.Id))!.Diagnosis);
    }

    [Theory, Trait("Case", "AUTHZ-02")]
    [InlineData("Nurse")]
    [InlineData("Admin")]
    [InlineData("Patient")]
    public async Task Only_a_doctor_can_write_clinical_content(string column)
    {
        var response = await _api.SendAsync(HttpMethod.Put, $"/api/MedicalRecord/{_recordOfB.Id}",
            TestIdentities.ForMatrixColumn(column), new MedicalRecordUpdateDto { Diagnosis = "rewritten" });

        await AssertRefusedWithoutDataAsync(response);
        Assert.Equal(_recordOfB.Diagnosis, (await _api.ReadMedicalRecordAsync(_recordOfB.Id))!.Diagnosis);
    }

    [Fact, Trait("Case", "AUTHZ-02")]
    public async Task Admin_cannot_read_clinical_content()
    {
        var single = await _api.SendAsync(HttpMethod.Get, $"/api/MedicalRecord/{_recordOfA.Id}", TestIdentities.Admin1);
        var list = await _api.SendAsync(HttpMethod.Get, "/api/MedicalRecord", TestIdentities.Admin1);

        await AssertRefusedWithoutDataAsync(single);
        await AssertRefusedWithoutDataAsync(list);
    }

    [Fact, Trait("Case", "BOLA-05")]
    public async Task Record_cannot_be_attached_to_another_patients_appointment()
    {
        // Documented rule: appointmentId must belong to the patient of the record.
        var appointmentOfB = await _api.CreateAppointmentAsync(_patientB.Id);

        var response = await _api.SendAsync(HttpMethod.Post, "/api/MedicalRecord", TestIdentities.Doctor1,
            new MedicalRecordCreateDto
            {
                PatientId = _patientA.Id,
                DoctorId = appointmentOfB.DoctorId,
                AppointmentId = appointmentOfB.Id,
                Diagnosis = "synthetic-cross-patient"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await _api.WithDbAsync(db =>
            Task.FromResult(db.MedicalRecords.Any(r => r.AppointmentId == appointmentOfB.Id))));
    }

    [Fact]
    public async Task Clinical_staff_still_get_not_found_for_a_missing_record()
    {
        // Doctors and nurses may read every record, so telling them an id does not exist discloses nothing.
        var response = await _api.SendAsync(HttpMethod.Get, $"/api/MedicalRecord/{int.MaxValue}", TestIdentities.Doctor1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact, Trait("Case", "BOLA-08")]
    public async Task Patient_cannot_tell_a_missing_record_from_another_patients_record()
    {
        const int missingId = int.MaxValue;
        Assert.Null(await _api.ReadMedicalRecordAsync(missingId));

        var missing = await _api.SendAsync(HttpMethod.Get, $"/api/MedicalRecord/{missingId}", TestIdentities.PatientA);
        var others = await _api.SendAsync(HttpMethod.Get, $"/api/MedicalRecord/{_recordOfB.Id}", TestIdentities.PatientA);

        Assert.Equal(others.StatusCode, missing.StatusCode);
        Assert.Equal(await others.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
    }
}
