using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagement.ApiTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Services.AuditService;

namespace HospitalManagement.ApiTests.Reliability;

/// <summary>
/// ARCHITECTURE D-8: a change and its audit entry commit together or not at all. Here the audit store fails for one
/// action; the change that triggered it must not be saved.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Reliability")]
public class TransactionalWriteTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;
    private ApiFactory _factory = null!;

    public TransactionalWriteTests(ApiFixture fixture) => _fixture = fixture;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_fixture.ConnectionString, services =>
        {
            services.RemoveAll<IAuditService>();
            services.AddScoped<IAuditService, FailingAuditService>();
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    [Trait("Risk", "R-013")]
    [Trait("Risk", "R-041")]
    public async Task AuditFails_ChangeIsRolledBack()
    {
        var data = new TestData(_fixture.Factory); // the normal host builds the data
        using var admin = _factory.ClientFor(TestUser.Admin());
        var name = "Rolled back " + Guid.NewGuid().ToString("N")[..8];

        var response = await admin.PostAsJsonAsync("/api/Department", new { name }, TestData.Json);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var reader = data.Admin;
        var departments = await reader.GetFromJsonAsync<JsonElement>("/api/Department", TestData.Json);
        Assert.DoesNotContain(departments.EnumerateArray(), d => d.GetProperty("name").GetString() == name);
    }

    [Fact]
    [Trait("Risk", "R-013")]
    public async Task ReadRequests_DoNotDependOnTheAudit()
    {
        using var admin = _factory.ClientFor(TestUser.Admin());

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/Department")).StatusCode);
    }

    /// <summary>Fails only for business changes; the login/provisioning audit of the user sync still works.</summary>
    private sealed class FailingAuditService : IAuditService
    {
        private readonly AuditService _inner;

        public FailingAuditService(Data.ApplicationDbContext context, Services.Common.ICurrentUserContext user, TimeProvider time) =>
            _inner = new AuditService(context, user, time);

        public Task LogAsync(string action, string? entityType = null, object? entityId = null, object? details = null,
            bool success = true, CancellationToken cancellationToken = default) =>
            action.StartsWith("User.")
                ? _inner.LogAsync(action, entityType, entityId, details, success, cancellationToken)
                : throw new InvalidOperationException("Simulated audit store failure");

        public Task<IReadOnlyList<Domain.Dtos.AuditLogDto>> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default) =>
            _inner.QueryAsync(query, cancellationToken);
    }
}
