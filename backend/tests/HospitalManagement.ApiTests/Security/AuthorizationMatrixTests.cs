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

    // One test per expected result, so no assertion is chosen by a branch (assertion rule 15).
    [Theory]
    [MemberData(nameof(AuthorizationMatrix.RefusedCases), MemberType = typeof(AuthorizationMatrix))]
    [Trait("Risk", "R-005")]
    [Trait("Risk", "R-015")]
    [Trait("Risk", "R-020")]
    [Trait("Risk", "R-069")]
    public async Task Endpoint_Identity_IsRefusedWithExactStatus(string method, string template, string identity, int expectedStatus)
    {
        using var response = await SendAsync(method, template, identity);

        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AuthorizationMatrix.AllowedCases), MemberType = typeof(AuthorizationMatrix))]
    [Trait("Risk", "R-005")]
    [Trait("Risk", "R-015")]
    [Trait("Risk", "R-020")]
    [Trait("Risk", "R-069")]
    public async Task Endpoint_Identity_PassesAuthorization(string method, string template, string identity)
    {
        using var response = await SendAsync(method, template, identity);

        // Assertion rule 1 exception: authorization is the behavior here, and the request is deliberately empty (an
        // empty body, a missing id). Passing authorization means a normal answer about the request itself: success,
        // 400, 404 or 409. A 5xx hid the result, so it does not count. The business results are asserted per
        // endpoint in the other API tests.
        var status = (int)response.StatusCode;
        Assert.True(status is not (401 or 403) && status < 500,
            $"{identity} must pass authorization on {method} {template} and get a normal answer, got {status}");
    }

    private async Task<HttpResponseMessage> SendAsync(string method, string template, string identity)
    {
        using var client = _factory.ClientFor(UserFor(identity));
        using var request = new HttpRequestMessage(new HttpMethod(method), template.Replace("{id}", MissingId.ToString(CultureInfo.InvariantCulture)));
        request.Content = method is "POST" or "PUT" ? new StringContent("{}", Encoding.UTF8, "application/json") : null;
        return await client.SendAsync(request);
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
