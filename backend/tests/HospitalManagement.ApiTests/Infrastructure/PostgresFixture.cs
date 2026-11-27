using Testcontainers.PostgreSql;

namespace HospitalManagement.ApiTests.Infrastructure;

/// <summary>
/// One real PostgreSQL container (same major version as docker-compose) and one API host per test run.
/// Isolation comes from data, not from separate databases: every test creates its own users, patients,
/// doctors and appointments with unique values and never relies on rows created by another test.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public ApiFactory Factory { get; private set; } = null!;

    /// <summary>For a test class that needs its own host with test doubles, on the same database.</summary>
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Factory = new ApiFactory(_container.GetConnectionString());
        // Start the host once so migrations run before the first test.
        using var client = Factory.CreateClient();
        (await client.GetAsync("/alive")).EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711", Justification = "xUnit names a collection definition this way")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
