using System.Net;
using api.Tests.Infrastructure;
using Domain.Dtos;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace api.Tests.Security;

/// <summary>
/// Role-level authorization (docs/qe-framework/SECURITY_TESTING.md section 7): every cell of the
/// role by endpoint matrix has a test, and every endpoint the API exposes is accounted for.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Gate", "G-SEC-01")]
public class AuthorizationMatrixTests
{
    private readonly ApiFactory _api;

    public AuthorizationMatrixTests(ApiFactory api) => _api = api;

    [Theory, Trait("Case", "AUTHN-01"), Trait("Case", "AUTHZ-01"), Trait("Case", "AUTHZ-02"), Trait("Case", "AUTHZ-03")]
    [MemberData(nameof(RoleByEndpointMatrix.Cells), MemberType = typeof(RoleByEndpointMatrix))]
    public async Task Matrix_cell_has_the_documented_outcome(string endpoint, string column)
    {
        var row = RoleByEndpointMatrix.Rows.Single(r => r.Endpoint == endpoint);
        var (url, body) = await row.Prepare(_api);
        var identity = TestIdentities.ForMatrixColumn(column);

        var response = await _api.SendAsync(row.Method, url, identity, body);
        var content = await response.Content.ReadAsStringAsync();

        if (column == "Anonymous")
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(string.Empty, content);
        }
        else if (row.Allowed.Contains(column))
        {
            Assert.True(response.StatusCode == row.Success,
                $"{column} is documented as allowed for {endpoint} (expected {(int)row.Success}) but got {(int)response.StatusCode}: {content}");
        }
        else
        {
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden,
                $"{column} is not documented as allowed for {endpoint} but got {(int)response.StatusCode}: {content}");
            // A refusal carries no data at all.
            Assert.Equal(string.Empty, content);
        }
    }

    [Fact]
    public void Every_endpoint_is_in_the_matrix_or_listed_as_not_yet_covered()
    {
        var exposed = _api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => (Endpoint: e, Action: e.Metadata.GetMetadata<ControllerActionDescriptor>()))
            .Where(x => x.Action != null)
            .SelectMany(x => x.Endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                .Select(method => (Key: $"{method} /{x.Endpoint.RoutePattern.RawText}", Controller: x.Action!.ControllerName)))
            .ToList();

        var inMatrix = RoleByEndpointMatrix.Rows.Select(r => r.Endpoint).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unaccounted = exposed
            .Where(e => !inMatrix.Contains(e.Key) && !RoleByEndpointMatrix.ControllersNotYetCovered.Contains(e.Controller))
            .Select(e => e.Key)
            .ToList();
        Assert.True(unaccounted.Count == 0,
            "Endpoints without a matrix row (add rows to RoleByEndpointMatrix): " + string.Join(", ", unaccounted));

        var exposedKeys = exposed.Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stale = inMatrix.Where(key => !exposedKeys.Contains(key)).ToList();
        Assert.True(stale.Count == 0,
            "Matrix rows for endpoints the API does not expose: " + string.Join(", ", stale));
    }

    [Fact, Trait("Case", "AUTHZ-04")]
    public async Task Caller_with_staff_and_patient_roles_is_treated_as_staff()
    {
        var response = await _api.SendAsync(HttpMethod.Get, "/api/Patient", TestIdentities.MultiRole);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Refused writes leave state unchanged (negative security test: status, no data, no state change). ----

    [Theory, Trait("Case", "AUTHZ-02")]
    [InlineData("Patient")]
    [InlineData("Nurse")]
    [InlineData("Doctor")]
    [InlineData("NoRoles")]
    public async Task Refused_delete_leaves_the_patient_in_place(string column)
    {
        var patient = await _api.CreatePatientAsync();

        var response = await _api.SendAsync(HttpMethod.Delete, $"/api/Patient/{patient.Id}",
            TestIdentities.ForMatrixColumn(column));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(await _api.ReadPatientAsync(patient.Id));
    }

    [Theory, Trait("Case", "AUTHZ-02")]
    [InlineData("Patient")]
    [InlineData("NoRoles")]
    public async Task Refused_update_leaves_the_patient_unchanged(string column)
    {
        var patient = await _api.CreatePatientAsync();
        var attempted = new PatientCreationDto
        {
            UserName = "changed-by-unauthorized-caller",
            Email = "changed@example.test",
            Phone = 5550199,
            ServiceId = patient.ServiceId
        };

        var response = await _api.SendAsync(HttpMethod.Put, $"/api/Patient/{patient.Id}",
            TestIdentities.ForMatrixColumn(column), attempted);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var stored = await _api.ReadPatientAsync(patient.Id);
        Assert.Equal(patient.UserName, stored!.UserName);
        Assert.Equal(patient.Email, stored.Email);
        Assert.Equal(patient.Phone, stored.Phone);
    }

    [Theory, Trait("Case", "AUTHZ-02")]
    [InlineData("Patient")]
    [InlineData("NoRoles")]
    public async Task Refused_create_adds_no_patient(string column)
    {
        var serviceId = await _api.CreateServiceAsync();
        var marker = $"synthetic-patient-{Guid.NewGuid():N}";

        var response = await _api.SendAsync(HttpMethod.Post, "/api/Patient",
            TestIdentities.ForMatrixColumn(column),
            new PatientCreationDto { UserName = marker, Email = "refused@example.test", Phone = 5550100, ServiceId = serviceId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await _api.WithDbAsync(db => Task.FromResult(db.Patients.Any(p => p.UserName == marker))));
    }

    [Theory, Trait("Case", "AUTHZ-08")]
    [InlineData("Patient")]
    [InlineData("Nurse")]
    [InlineData("Doctor")]
    public async Task Only_an_admin_can_link_an_account_to_a_patient_record(string column)
    {
        // The privilege-gain abuse case: a caller links their own account to someone's record.
        var caller = TestIdentities.ForMatrixColumn(column)!;
        var callerProfile = await _api.ProvisionAsync(caller);
        var victim = await _api.CreatePatientAsync();

        var response = await _api.SendAsync(HttpMethod.Put, $"/api/User/{callerProfile.Id}/link",
            caller, new UserLinkDto { PatientId = victim.Id });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null((await _api.ReadPatientAsync(victim.Id))!.UserId);
    }

    [Theory, Trait("Case", "AUTHZ-08")]
    [InlineData("Patient")]
    [InlineData("Nurse")]
    [InlineData("Doctor")]
    public async Task Only_an_admin_can_deactivate_an_account(string column)
    {
        var target = await _api.ProvisionAsync(TestIdentities.AdminTarget);

        var response = await _api.SendAsync(HttpMethod.Put, $"/api/User/{target.Id}/status",
            TestIdentities.ForMatrixColumn(column), new UserStatusDto { IsActive = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True((await _api.ProvisionAsync(TestIdentities.AdminTarget)).IsActive);
    }
}
