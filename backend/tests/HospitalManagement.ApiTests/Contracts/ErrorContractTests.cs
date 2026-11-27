using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagement.ApiTests.Infrastructure;

namespace HospitalManagement.ApiTests.Contracts;

/// <summary>
/// R-061: the API's error responses have one format, described in contracts/openapi.json, so the web frontend can
/// show their message. Every 400, 404 and 409, and a 403 that explains itself, is ProblemDetails
/// (<c>application/problem+json</c>) with the message in <c>detail</c>. A 403 from an ownership or role check stays
/// empty, so it reveals nothing (R-004; asserted in BolaTests).
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Contracts")]
public class ErrorContractTests
{
    private const int Missing = int.MaxValue;
    private readonly ApiFactory _factory;
    private readonly TestData _data;

    public ErrorContractTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
        _data = new TestData(fixture.Factory);
    }

    [Theory]
    [InlineData("GET", "/api/Doctor/{0}")]
    [InlineData("GET", "/api/Patient/{0}")]
    [InlineData("GET", "/api/Service/{0}")]
    [InlineData("GET", "/api/Department/{0}")]
    [InlineData("GET", "/api/Appointment/{0}")]
    [InlineData("DELETE", "/api/Doctor/{0}")]
    [InlineData("DELETE", "/api/Service/{0}")]
    [Trait("Risk", "R-061")]
    public async Task MissingRecord_Returns404ProblemDetails_WithMessage(string method, string pathFormat)
    {
        using var admin = _data.Admin;

        using var request = new HttpRequestMessage(new HttpMethod(method), string.Format(CultureInfo.InvariantCulture, pathFormat, Missing));
        var response = await admin.SendAsync(request);

        var problem = await AssertProblemAsync(response, HttpStatusCode.NotFound);
        Assert.Contains("not found", problem.GetProperty("detail").GetString());
    }

    [Fact]
    [Trait("Risk", "R-061")]
    public async Task BusinessRuleBroken_Returns400ProblemDetails_WithMessage()
    {
        using var admin = _data.Admin;

        var response = await admin.PostAsJsonAsync("/api/Doctor",
            new { firstName = "Test", lastName = "Doctor", serviceIds = new[] { Missing } }, TestData.Json);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("One or more service IDs do not exist", problem.GetProperty("detail").GetString());
    }

    [Fact]
    [Trait("Risk", "R-061")]
    public async Task InvalidBody_Returns400ProblemDetails_WithFieldErrors()
    {
        using var admin = _data.Admin;

        var response = await admin.PostAsJsonAsync("/api/Service", new { description = "no name" }, TestData.Json);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Name", out _), "the field errors must name the missing Name");
    }

    [Fact]
    [Trait("Risk", "R-061")]
    public async Task DeleteServiceStillInUse_Returns409ProblemDetails_WithMessage()
    {
        await _data.CreatePatientAsync(); // registered to the shared test service
        using var admin = _data.Admin;

        var response = await admin.DeleteAsync($"/api/Service/{await _data.ServiceIdAsync()}");

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("The service is still used by patients or doctors", problem.GetProperty("detail").GetString());
    }

    [Fact]
    [Trait("Risk", "R-061")]
    public async Task UnlinkedDoctorWritesRecord_Returns403ProblemDetails_WithReason()
    {
        var patientId = await _data.CreatePatientAsync();
        using var doctor = _factory.ClientFor(TestUser.Doctor());

        var response = await doctor.PostAsJsonAsync("/api/MedicalRecord",
            new { patientId, diagnosis = "Synthetic" }, TestData.Json);

        var problem = await AssertProblemAsync(response, HttpStatusCode.Forbidden);
        Assert.Contains("not linked to a doctor profile", problem.GetProperty("detail").GetString());
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestData.Json);
        Assert.Equal((int)expected, problem.GetProperty("status").GetInt32());
        return problem;
    }
}
