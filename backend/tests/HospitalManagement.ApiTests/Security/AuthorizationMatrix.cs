namespace HospitalManagement.ApiTests.Security;

/// <summary>
/// The role-by-endpoint matrix of docs/testing/SECURITY_TESTING.md §2, in code. One row per endpoint.
/// "Own" (a patient may reach only their own record) is not a role decision; it is tested by the BOLA
/// tests, so the patient column of those rows is checked there and not here.
/// </summary>
public static class AuthorizationMatrix
{
    public sealed record Row(string Method, string Template, string AllowedRoles, bool PatientOwnOnly = false);

    // A = Admin, D = Doctor, N = Nurse, P = Patient
    public static readonly Row[] Rows =
    {
        new("GET", "/api/Appointment", "ADN", PatientOwnOnly: true),
        new("GET", "/api/Appointment/{id}", "ADN", PatientOwnOnly: true),
        new("POST", "/api/Appointment", "ADN", PatientOwnOnly: true),
        new("PUT", "/api/Appointment/{id}", "ADN"),
        new("POST", "/api/Appointment/{id}/cancel", "ADN", PatientOwnOnly: true),
        new("POST", "/api/Appointment/{id}/correct-status", "A"),
        new("DELETE", "/api/Appointment/{id}", "A"),

        new("GET", "/api/AuditLog", "A"),

        new("GET", "/api/Department", "ADNP"),
        new("GET", "/api/Department/{id}", "ADNP"),
        new("POST", "/api/Department", "A"),
        new("PUT", "/api/Department/{id}", "A"),
        new("DELETE", "/api/Department/{id}", "A"),

        new("GET", "/api/Doctor", "ADNP"),
        new("GET", "/api/Doctor/{id}", "ADNP"),
        new("POST", "/api/Doctor", "A"),
        new("PUT", "/api/Doctor/{id}", "A"),
        new("DELETE", "/api/Doctor/{id}", "A"),

        new("GET", "/api/MedicalRecord", "DN", PatientOwnOnly: true),
        new("GET", "/api/MedicalRecord/{id}", "DN", PatientOwnOnly: true),
        new("POST", "/api/MedicalRecord", "D"),
        new("PUT", "/api/MedicalRecord/{id}", "D"),
        new("GET", "/api/MedicalRecord/{id}/history", "DN"),

        new("GET", "/api/Patient", "ADN"),
        new("GET", "/api/Patient/{id}", "ADN", PatientOwnOnly: true),
        // Q-017, Q-024: a doctor neither registers nor edits patients (R-069); a nurse or an administrator does (F-02).
        new("POST", "/api/Patient", "AN"),
        new("PUT", "/api/Patient/{id}", "AN"),
        new("DELETE", "/api/Patient/{id}", "A"),

        new("GET", "/api/Service", "ADNP"),
        new("GET", "/api/Service/{id}", "ADNP"),
        new("POST", "/api/Service", "A"),
        new("PUT", "/api/Service/{id}", "A"),
        new("DELETE", "/api/Service/{id}", "A"),

        new("GET", "/api/User/me", "ADNP"),
        new("GET", "/api/User", "A"),
        new("GET", "/api/User/{id}", "A"),
        new("PUT", "/api/User/{id}/status", "A"),
        new("PUT", "/api/User/{id}/link", "A"),
    };

    public static readonly string[] Identities = { "Anonymous", "NoRole", "Admin", "Doctor", "Nurse", "Patient" };

    public static IEnumerable<object[]> Cases()
    {
        foreach (var row in Rows)
        foreach (var identity in Identities)
        {
            if (identity == "Patient" && row.PatientOwnOnly) continue;
            yield return new object[] { row.Method, row.Template, identity, Expected(row, identity) };
        }
    }

    /// <summary>"401", "403", or "allowed" (anything but 401/403: the role passed authorization).</summary>
    private static string Expected(Row row, string identity) => identity switch
    {
        "Anonymous" => "401",
        "NoRole" => "403",
        _ => row.AllowedRoles.Contains(identity[0]) ? "allowed" : "403"
    };
}
