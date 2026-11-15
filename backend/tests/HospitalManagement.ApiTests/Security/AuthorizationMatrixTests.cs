using System.Net;
using System.Text;
using HospitalManagement.ApiTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalManagement.ApiTests.Security;

[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public class AuthorizationMatrixTests
{
    private const int MissingId = 999_999;
    private readonly ApiFactory _factory;

    public AuthorizationMatrixTests(ApiFixture fixture) => _factory = fixture.Factory;

    [Theory]
    [MemberData(nameof(AuthorizationMatrix.Cases), MemberType = typeof(AuthorizationMatrix))]
    [Trait("Risk", "R-005")]
    [Trait("Risk", "R-015")]
    [Trait("Risk", "R-020")]
    public async Task Endpoint_Identity_GetsExpectedAuthorizationResult(string method, string template, string identity, string expected)
    {
        using var client = _factory.ClientFor(UserFor(identity));
        var request = new HttpRequestMessage(new HttpMethod(method), template.Replace("{id}", MissingId.ToString()));
        if (method is "POST" or "PUT")
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        var status = (int)response.StatusCode;
        switch (expected)
        {
            case "401": Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); break;
            case "403": Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); break;
            default:
                Assert.True(status != 401 && status != 403,
                    $"{identity} must pass authorization on {method} {template}, got {status}");
                break;
        }
    }

    /// <summary>A new endpoint without a matrix row fails here, so the matrix cannot fall behind the code.</summary>
    [Fact]
    [Trait("Risk", "R-005")]
    public void EveryControllerEndpoint_HasAMatrixRow()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() != null)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? Array.Empty<string>())
                .Select(m => $"{m} /{e.RoutePattern.RawText}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matrix = AuthorizationMatrix.Rows.Select(r => $"{r.Method} {r.Template}").ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Empty(endpoints.Except(matrix));
        Assert.Empty(matrix.Except(endpoints));
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task HealthEndpoints_AreAnonymous(string path)
    {
        using var client = _factory.ClientFor(null);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    private static TestUser? UserFor(string identity) => identity switch
    {
        "Anonymous" => null,
        "NoRole" => TestUser.NoRole(),
        "Admin" => TestUser.Admin(),
        "Doctor" => TestUser.Doctor(),
        "Nurse" => TestUser.Nurse(),
        "Patient" => TestUser.Patient(),
        _ => throw new ArgumentOutOfRangeException(nameof(identity))
    };
}
