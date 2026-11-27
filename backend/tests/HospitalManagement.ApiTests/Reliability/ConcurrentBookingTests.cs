using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagement.ApiTests.Infrastructure;

namespace HospitalManagement.ApiTests.Reliability;

/// <summary>
/// R-048: bookings that arrive at the same moment. The service checks "is the slot free?" and then inserts; two
/// requests can both pass the check before either inserts. Only the database can decide between them, so this
/// runs against real PostgreSQL with requests in parallel (BUSINESS_FLOWS F-03.9).
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Reliability")]
public class ConcurrentBookingTests
{
    private const int Parallel = 10;

    private readonly ApiFactory _factory;
    private readonly TestData _data;

    public ConcurrentBookingTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
        _data = new TestData(_factory);
    }

    [Fact]
    [Trait("Risk", "R-048")]
    [Trait("Scenario", "F-03.9")]
    public async Task SameSlotBookedInParallel_ExactlyOneSucceeds_TheOthersGet409()
    {
        var (_, doctorId) = await _data.LinkedDoctorAsync();
        var patientIds = new List<int>();
        for (var i = 0; i < Parallel; i++) patientIds.Add(await _data.CreatePatientAsync());
        var slot = DateTime.UtcNow.Date.AddDays(40).AddHours(9).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var clients = patientIds.Select(_ => _factory.ClientFor(TestUser.Admin())).ToList();

        var responses = await Task.WhenAll(clients.Select((client, i) => client.PostAsJsonAsync("/api/Appointment",
            new { patientId = patientIds[i], doctorId, scheduledAt = slot, durationMinutes = 30 }, TestData.Json)));

        var statuses = string.Join(", ", responses.Select(r => (int)r.StatusCode).OrderBy(s => s));
        Assert.Equal("201, " + string.Join(", ", Enumerable.Repeat("409", Parallel - 1)), statuses);
        using var admin = _data.Admin;
        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/Appointment?doctorId={doctorId}", TestData.Json);
        Assert.Equal(1, stored.GetArrayLength());
        clients.ForEach(c => c.Dispose());
    }

    [Fact]
    [Trait("Risk", "R-048")]
    [Trait("Scenario", "F-03.9")]
    public async Task OverlappingSlotsBookedInParallel_ExactlyOneSucceeds()
    {
        // 09:00–09:30 and 09:15–09:45 overlap without starting at the same time.
        var (_, doctorId) = await _data.LinkedDoctorAsync();
        var first = await _data.CreatePatientAsync();
        var second = await _data.CreatePatientAsync();
        var day = DateTime.UtcNow.Date.AddDays(41).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        using var a = _factory.ClientFor(TestUser.Admin());
        using var b = _factory.ClientFor(TestUser.Admin());

        var responses = await Task.WhenAll(
            a.PostAsJsonAsync("/api/Appointment",
                new { patientId = first, doctorId, scheduledAt = $"{day}T09:00:00Z", durationMinutes = 30 }, TestData.Json),
            b.PostAsJsonAsync("/api/Appointment",
                new { patientId = second, doctorId, scheduledAt = $"{day}T09:15:00Z", durationMinutes = 30 }, TestData.Json));

        var statuses = string.Join(", ", responses.Select(r => (int)r.StatusCode).OrderBy(s => s));
        Assert.Equal("201, 409", statuses);
        // The invariant is in the database, not in the status codes (assertion rule 20).
        using var admin = _data.Admin;
        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/Appointment?doctorId={doctorId}", TestData.Json);
        Assert.Equal(1, stored.GetArrayLength());
    }
}
