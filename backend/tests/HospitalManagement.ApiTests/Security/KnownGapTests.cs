using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagement.ApiTests.Infrastructure;

namespace HospitalManagement.ApiTests.Security;

/// <summary>
/// Tests for risks that are still open (QUALITY_GATES §1 "Known gaps"). Each asserts the correct behavior and
/// is skipped until the fix lands; the fix removes the Skip and the KnownGap trait.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public class KnownGapTests
{
    private readonly ApiFactory _factory;
    private readonly TestData _data;

    public KnownGapTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
        _data = new TestData(_factory);
    }

    [Fact(Skip = "Known gap R-051")]
    [Trait("Risk", "R-051")]
    [Trait("KnownGap", "R-051")]
    [Trait("Scenario", "F-03.10")]
    public async Task Booking_LocalTimeWithoutOffset_IsRejected()
    {
        var (_, doctorId) = await _data.LinkedDoctorAsync();
        var patientId = await _data.CreatePatientAsync();
        using var admin = _data.Admin;
        var localWithoutOffset = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd'T'09:00:00");

        var response = await admin.PostAsJsonAsync("/api/Appointment", new
        {
            patientId, doctorId, scheduledAt = localWithoutOffset, durationMinutes = 30
        }, TestData.Json);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
