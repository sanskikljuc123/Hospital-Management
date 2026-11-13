using Domain.Constants;

namespace api.Tests.Infrastructure;

/// <summary>
/// A synthetic identity that exists only in tests. The subject is fixed so that the API's
/// local user profile is the same row on every request of that identity.
/// </summary>
public sealed record TestIdentity(string Name, string Subject, params string[] Roles)
{
    public string SessionId => $"session-{Name}";
    public override string ToString() => Name;
}

/// <summary>
/// Test identities required by docs/qe-framework/TEST_DATA_AND_ENVIRONMENTS.md (TI-01 to TI-05):
/// two per role, one without roles, one to be deactivated, linked and unlinked patients, one multi-role.
/// </summary>
public static class TestIdentities
{
    public static readonly TestIdentity Admin1 = new("test-admin-1", "00000000-0000-4000-a000-000000000001", Roles.Admin);
    public static readonly TestIdentity Admin2 = new("test-admin-2", "00000000-0000-4000-a000-000000000002", Roles.Admin);
    public static readonly TestIdentity Doctor1 = new("test-doctor-1", "00000000-0000-4000-a000-000000000011", Roles.Doctor);
    public static readonly TestIdentity Doctor2 = new("test-doctor-2", "00000000-0000-4000-a000-000000000012", Roles.Doctor);
    public static readonly TestIdentity Nurse1 = new("test-nurse-1", "00000000-0000-4000-a000-000000000021", Roles.Nurse);
    public static readonly TestIdentity Nurse2 = new("test-nurse-2", "00000000-0000-4000-a000-000000000022", Roles.Nurse);

    /// <summary>Patient-role identity linked to patient record A.</summary>
    public static readonly TestIdentity PatientA = new("test-patient-a", "00000000-0000-4000-a000-000000000031", Roles.Patient);
    /// <summary>Patient-role identity linked to patient record B.</summary>
    public static readonly TestIdentity PatientB = new("test-patient-b", "00000000-0000-4000-a000-000000000032", Roles.Patient);
    /// <summary>Patient-role identity that no admin has linked to a record.</summary>
    public static readonly TestIdentity PatientUnlinked = new("test-patient-unlinked", "00000000-0000-4000-a000-000000000033", Roles.Patient);

    public static readonly TestIdentity NoRoles = new("test-no-roles", "00000000-0000-4000-a000-000000000041");
    public static readonly TestIdentity MultiRole = new("test-nurse-patient", "00000000-0000-4000-a000-000000000042", Roles.Nurse, Roles.Patient);
    /// <summary>Only ever used as the target of admin operations (status, link), never as a caller.</summary>
    public static readonly TestIdentity AdminTarget = new("test-admin-target", "00000000-0000-4000-a000-000000000043", Roles.Patient);

    /// <summary>The first identity of each role, used for the role by endpoint matrix.</summary>
    public static TestIdentity? ForMatrixColumn(string column) => column switch
    {
        "Anonymous" => null,
        "NoRoles" => NoRoles,
        Roles.Patient => PatientUnlinked,
        Roles.Nurse => Nurse1,
        Roles.Doctor => Doctor1,
        Roles.Admin => Admin1,
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unknown matrix column")
    };
}
