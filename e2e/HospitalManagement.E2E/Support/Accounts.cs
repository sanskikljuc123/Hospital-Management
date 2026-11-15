namespace HospitalManagement.E2E.Support;

/// <summary>The sample users of the realm file (keycloak/realms/HospitalRealm-realm.json).</summary>
public static class Accounts
{
    public static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        ["admin.user"] = "Alex Admin",
        ["doctor.user"] = "Dana Doctor",
        ["nurse.user"] = "Noel Nurse",
        ["patient.user"] = "Pat Patient",
    };
}
