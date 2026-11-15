namespace HospitalManagement.ApiTests.Infrastructure;

/// <summary>
/// A synthetic identity as Keycloak would describe it in a token. Every test creates its own users with
/// unique subjects, so no test depends on another test's data.
/// </summary>
public sealed record TestUser(string Subject, string UserName, IReadOnlyList<string> Roles)
{
    public static TestUser Create(string prefix, params string[] roles)
    {
        var id = Guid.NewGuid().ToString("N")[..10];
        return new TestUser($"sub-{prefix}-{id}", $"{prefix}.{id}", roles);
    }

    public static TestUser Admin() => Create("admin", "Admin");
    public static TestUser Doctor() => Create("doctor", "Doctor");
    public static TestUser Nurse() => Create("nurse", "Nurse");
    public static TestUser Patient() => Create("patient", "Patient");

    /// <summary>Valid token, but only Keycloak's built-in roles: no hospital role.</summary>
    public static TestUser NoRole() => Create("norole", "offline_access", "default-roles-hospitalrealm");
}
