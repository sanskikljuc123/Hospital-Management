using System.Globalization;
using System.Net;
using System.Text;
using HospitalManagement.ApiTests.Infrastructure;
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
    [Trait("Risk", "R-069")]
    public async Task Endpoint_Identity_GetsExpectedAuthorizationResult(string method, string template, string identity, string expected)
    {
        using var client = _factory.ClientFor(UserFor(identity));
        var request = new HttpRequestMessage(new HttpMethod(method), template.Replace("{id}", MissingId.ToString(CultureInfo.InvariantCulture)));
        if (method is "POST" or "PUT")
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        var status = (int)response.StatusCode;
        switch (expected)
        {
            case "401": Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); break;
            case "403": Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); break;
            default:
                // Passing authorization means the request reached the endpoint and got a normal answer: success,
                // or a 4xx about the request itself (400 for the empty body, 404 for the missing id, 409).
                // A 5xx is a server failure that hid the result, so it does not count as "allowed".
                Assert.True(status is not (401 or 403) && status < 500,
                    $"{identity} must pass authorization on {method} {template} and get a normal answer, got {status}");
                break;
        }
    }

    /// <summary>The anonymous endpoints outside the matrix, tested by <see cref="HealthEndpoints_AreAnonymous"/>.</summary>
    private static readonly string[] HealthEndpoints = ["ANY /health", "ANY /alive"];

    /// <summary>
    /// A new endpoint without a matrix row fails here, so the matrix cannot fall behind the code. Every routed
    /// endpoint counts, not only controller actions: one added with <c>app.MapGet</c> needs a row too. An endpoint
    /// that accepts any method (health checks) is listed as <c>ANY</c>.
    /// </summary>
    [Fact]
    [Trait("Risk", "R-005")]
    public void EveryEndpoint_HasAMatrixRow()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods is { Count: > 0 } methods
                    ? methods
                    : ["ANY"])
                .Select(m => $"{m} /{e.RoutePattern.RawText?.TrimStart('/')}"))
            .Except(HealthEndpoints, StringComparer.OrdinalIgnoreCase)
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
