using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagement.ApiTests.Infrastructure;

namespace HospitalManagement.ApiTests.Contracts;

/// <summary>
/// R-051: a time must say which time zone it is in. Without one, "09:00" could mean 09:00 UTC or 09:00 at the
/// clinic, and reading it as UTC shifts the appointment (BUSINESS_FLOWS F-03.10). Times are stored in UTC.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Contracts")]
public class TimeZoneTests
{
    private readonly TestData _data;

    public TimeZoneTests(ApiFixture fixture) => _data = new TestData(fixture.Factory);

    [Fact]
    [Trait("Risk", "R-051")]
    [Trait("Scenario", "F-03.10")]
    public async Task Booking_LocalTimeWithoutOffset_IsRejected_AndNothingIsBooked()
    {
        var (_, doctorId) = await _data.LinkedDoctorAsync();
        var patientId = await _data.CreatePatientAsync();
        using var admin = _data.Admin;
        var localWithoutOffset = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd'T'09:00:00", CultureInfo.InvariantCulture);

        var response = await admin.PostAsJsonAsync("/api/Appointment", new
        {
            patientId, doctorId, scheduledAt = localWithoutOffset, durationMinutes = 30
        }, TestData.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var appointments = await admin.GetFromJsonAsync<JsonElement>($"/api/Appointment?doctorId={doctorId}", TestData.Json);
        Assert.Equal(0, appointments.GetArrayLength());
    }

    [Fact]
    [Trait("Risk", "R-051")]
    [Trait("Scenario", "F-03.10")]
    public async Task Booking_TimeWithOffset_IsStoredAsTheSameInstantInUtc()
    {
        var (_, doctorId) = await _data.LinkedDoctorAsync();
        var patientId = await _data.CreatePatientAsync();
        using var admin = _data.Admin;
        var day = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var response = await admin.PostAsJsonAsync("/api/Appointment", new
        {
            patientId, doctorId, scheduledAt = $"{day}T09:00:00+01:00", durationMinutes = 30
        }, TestData.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = (await response.Content.ReadFromJsonAsync<JsonElement>(TestData.Json)).GetProperty("scheduledAt");
        Assert.Equal($"{day}T08:00:00Z", stored.GetString());
    }
}
