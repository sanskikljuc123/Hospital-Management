using System.Net;
using System.Net.Http.Json;
using api.Tests.Infrastructure;
using Domain.Dtos;
using Domain.Entities;

namespace api.Tests.Security;

/// <summary>
/// Object-level authorization for patient records (docs/qe-framework/SECURITY_TESTING.md section 8).
/// Ownership rule, from docs/API_ENDPOINTS.md ("Own data"): a user with only the Patient role may
/// read the one patient record an admin linked to their account, and nothing else.
/// Uses two Patient-role identities, each linked to their own record.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Gate", "G-SEC-02")]
public class BolaTests : IAsyncLifetime
{
    private readonly ApiFactory _api;
    private Patient _recordA = null!;
    private Patient _recordB = null!;

    public BolaTests(ApiFactory api) => _api = api;

    public async Task InitializeAsync()
    {
        // Fresh records per test; linking replaces any link left by an earlier test.
        _recordA = await _api.CreatePatientAsync();
        _recordB = await _api.CreatePatientAsync();
        await _api.LinkToPatientAsync(TestIdentities.PatientA, _recordA.Id);
        await _api.LinkToPatientAsync(TestIdentities.PatientB, _recordB.Id);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task AssertRefusedWithoutDataAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact, Trait("Case", "BOLA-01")]
    public async Task Patient_reads_their_own_record()
    {
        var response = await _api.SendAsync(HttpMethod.Get, $"/api/Patient/{_recordA.Id}", TestIdentities.PatientA);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Patient>();
        Assert.Equal(_recordA.Id, body!.Id);
        Assert.Equal(_recordA.Email, body.Email);
    }

    [Fact, Trait("Case", "BOLA-02")]
    public async Task Patient_cannot_read_another_patients_record()
    {
        var response = await _api.SendAsync(HttpMethod.Get, $"/api/Patient/{_recordB.Id}", TestIdentities.PatientA);

        await AssertRefusedWithoutDataAsync(response);
    }

    [Fact, Trait("Case", "BOLA-02")]
    public async Task Ownership_holds_in_both_directions()
    {
        // Guards against a check that happens to refuse A but would let B through.
        var response = await _api.SendAsync(HttpMethod.Get, $"/api/Patient/{_recordA.Id}", TestIdentities.PatientB);

        await AssertRefusedWithoutDataAsync(response);
    }

    [Fact, Trait("Case", "BOLA-03")]
    public async Task Unlinked_patient_cannot_read_any_record()
    {
        var response = await _api.SendAsync(HttpMethod.Get, $"/api/Patient/{_recordA.Id}", TestIdentities.PatientUnlinked);

        await AssertRefusedWithoutDataAsync(response);
    }

    [Fact, Trait("Case", "BOLA-03")]
    public async Task Identity_without_roles_cannot_read_any_record()
    {
        var response = await _api.SendAsync(HttpMethod.Get, $"/api/Patient/{_recordA.Id}", TestIdentities.NoRoles);

        await AssertRefusedWithoutDataAsync(response);
    }

    [Fact, Trait("Case", "BOLA-04")]
    public async Task Patient_cannot_list_patients()
    {
        var response = await _api.SendAsync(HttpMethod.Get, "/api/Patient", TestIdentities.PatientA);

        await AssertRefusedWithoutDataAsync(response);
    }

    [Fact, Trait("Case", "BOLA-06")]
    public async Task Patient_cannot_update_another_patients_record()
    {
        var response = await _api.SendAsync(HttpMethod.Put, $"/api/Patient/{_recordB.Id}", TestIdentities.PatientA,
            new PatientCreationDto { UserName = "taken-over", Email = "taken-over@example.test", Phone = 5550199, ServiceId = _recordB.ServiceId });

        await AssertRefusedWithoutDataAsync(response);
        var stored = await _api.ReadPatientAsync(_recordB.Id);
        Assert.Equal(_recordB.UserName, stored!.UserName);
        Assert.Equal(_recordB.Email, stored.Email);
    }

    [Fact, Trait("Case", "BOLA-06")]
    public async Task Patient_cannot_delete_another_patients_record()
    {
        var response = await _api.SendAsync(HttpMethod.Delete, $"/api/Patient/{_recordB.Id}", TestIdentities.PatientA);

        await AssertRefusedWithoutDataAsync(response);
        Assert.NotNull(await _api.ReadPatientAsync(_recordB.Id));
    }

    [Fact, Trait("Case", "BOLA-08")]
    public async Task Patient_cannot_tell_a_missing_record_from_another_patients_record()
    {
        const int missingId = int.MaxValue;
        Assert.Null(await _api.ReadPatientAsync(missingId));

        var missing = await _api.SendAsync(HttpMethod.Get, $"/api/Patient/{missingId}", TestIdentities.PatientA);
        var others = await _api.SendAsync(HttpMethod.Get, $"/api/Patient/{_recordB.Id}", TestIdentities.PatientA);

        Assert.Equal(others.StatusCode, missing.StatusCode);
        Assert.Equal(await others.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
    }

    [Fact, Trait("Case", "BOLA-02")]
    public async Task Access_follows_the_link_when_an_admin_moves_it()
    {
        // After an admin relinks A's account to a new record, the old record is no longer A's.
        var newRecord = await _api.CreatePatientAsync();
        await _api.LinkToPatientAsync(TestIdentities.PatientA, newRecord.Id);

        var oldRecord = await _api.SendAsync(HttpMethod.Get, $"/api/Patient/{_recordA.Id}", TestIdentities.PatientA);
        var current = await _api.SendAsync(HttpMethod.Get, $"/api/Patient/{newRecord.Id}", TestIdentities.PatientA);

        await AssertRefusedWithoutDataAsync(oldRecord);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
    }

    [Theory]
    [InlineData("Nurse")]
    [InlineData("Doctor")]
    [InlineData("Admin")]
    public async Task Staff_read_any_patient_record_by_design(string column)
    {
        // Documented exemption from BOLA-02: staff roles may read all patient records.
        var response = await _api.SendAsync(HttpMethod.Get, $"/api/Patient/{_recordB.Id}",
            TestIdentities.ForMatrixColumn(column));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
