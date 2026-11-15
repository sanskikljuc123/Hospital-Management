using System.Net;
using System.Net.Http.Json;
using HospitalManagement.ApiTests.Infrastructure;

namespace HospitalManagement.ApiTests.Security;

/// <summary>Deactivated accounts, SECURITY_TESTING §5; BUSINESS_FLOWS F-12.1.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public class AccountStateTests
{
    private readonly ApiFactory _factory;
    private readonly TestData _data;

    public AccountStateTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
        _data = new TestData(_factory);
    }

    [Theory]
    [InlineData("/api/User/me")]
    [InlineData("/api/Doctor")]
    [InlineData("/api/Appointment")]
    [Trait("Risk", "R-007")]
    [Trait("Scenario", "F-12.1")]
    public async Task DeactivatedUser_NextRequest_Returns403AccountDeactivated(string path)
    {
        var nurse = TestUser.Nurse();
        var userId = await _data.ProvisionAsync(nurse);
        using var admin = _data.Admin;
        (await admin.PutAsJsonAsync($"/api/User/{userId}/status", new { isActive = false }, TestData.Json))
            .EnsureSuccessStatusCode();
        using var client = _factory.ClientFor(nurse);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Account deactivated", await response.Content.ReadAsStringAsync());
    }
}
