using System.Net;
using api.Tests.Infrastructure;
using Domain.Constants;
using Domain.Dtos;

namespace api.Tests.Security;

/// <summary>
/// The role by endpoint matrix (docs/qe-framework/SECURITY_TESTING.md section 7.1) as data.
/// Expected outcomes come from docs/API_ENDPOINTS.md, not from the controller attributes.
/// </summary>
public static class RoleByEndpointMatrix
{
    /// <summary>Matrix columns: no identity, an identity without roles, then each realm role.</summary>
    public static readonly string[] Columns = { "Anonymous", "NoRoles", Roles.Patient, Roles.Nurse, Roles.Doctor, Roles.Admin };

    private static readonly string[] None = Array.Empty<string>();
    private static readonly string[] Staff = { Roles.Nurse, Roles.Doctor, Roles.Admin };
    private static readonly string[] AdminOnly = { Roles.Admin };
    private static readonly string[] DoctorOnly = { Roles.Doctor };
    private static readonly string[] ClinicalStaff = { Roles.Nurse, Roles.Doctor };
    /// <summary>"Any" in docs/API_ENDPOINTS.md: any authenticated user.</summary>
    private static readonly string[] Any = { "NoRoles", Roles.Patient, Roles.Nurse, Roles.Doctor, Roles.Admin };
    /// <summary>The Patient column's outcome depends on the object requested; see the BOLA test classes.</summary>
    private static readonly string[] PatientOwnOnly = { Roles.Patient };

    /// <param name="Endpoint">HTTP method and route template, exactly as the API declares it.</param>
    /// <param name="Allowed">Columns that must succeed. Every other column must be refused.</param>
    /// <param name="ObjectLevel">Columns whose outcome depends on the object requested; covered by the BOLA tests instead.</param>
    /// <param name="Prepare">Creates the state the request needs and returns the concrete URL and body.</param>
    /// <param name="Success">The documented success status for allowed columns.</param>
    public sealed record Row(
        string Endpoint,
        string[] Allowed,
        string[] ObjectLevel,
        Func<ApiFactory, Task<(string Url, object? Body)>> Prepare,
        HttpStatusCode Success = HttpStatusCode.OK)
    {
        public HttpMethod Method => new(Endpoint.Split(' ')[0]);
    }

    private static Func<ApiFactory, Task<(string, object?)>> Fixed(string url) =>
        _ => Task.FromResult<(string, object?)>((url, null));

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static PatientCreationDto NewPatientBody(int serviceId) => new()
    {
        UserName = Unique("synthetic-patient"),
        Email = $"{Unique("patient")}@example.test",
        Phone = 5550100,
        ServiceId = serviceId
    };

    private static DoctorCreationDto NewDoctorBody() => new()
    {
        FirstName = "Synthetic",
        LastName = Unique("doctor"),
        Address = "1 Example Street"
    };

    public static readonly IReadOnlyList<Row> Rows = new List<Row>
    {
        // ---- Patients ----
        new("GET /api/Patient", Staff, None, Fixed("/api/Patient")),
        new("GET /api/Patient/{id}", Staff, PatientOwnOnly,
            async api => ($"/api/Patient/{(await api.CreatePatientAsync()).Id}", null)),
        new("POST /api/Patient", Staff, None,
            async api => ("/api/Patient", NewPatientBody(await api.CreateServiceAsync()))),
        new("PUT /api/Patient/{id}", Staff, None,
            async api =>
            {
                var patient = await api.CreatePatientAsync();
                return ($"/api/Patient/{patient.Id}", NewPatientBody(patient.ServiceId));
            }),
        new("DELETE /api/Patient/{id}", AdminOnly, None,
            async api => ($"/api/Patient/{(await api.CreatePatientAsync()).Id}", null)),

        // ---- Users ----
        new("GET /api/User/me", Any, None, Fixed("/api/User/me")),
        new("GET /api/User", AdminOnly, None, Fixed("/api/User")),
        new("GET /api/User/{id}", AdminOnly, None,
            async api => ($"/api/User/{(await api.ProvisionAsync(TestIdentities.AdminTarget)).Id}", null)),
        new("PUT /api/User/{id}/status", AdminOnly, None,
            async api => ($"/api/User/{(await api.ProvisionAsync(TestIdentities.AdminTarget)).Id}/status",
                new UserStatusDto { IsActive = true })),
        new("PUT /api/User/{id}/link", AdminOnly, None,
            async api => ($"/api/User/{(await api.ProvisionAsync(TestIdentities.AdminTarget)).Id}/link",
                new UserLinkDto())),

        // ---- Services ----
        new("GET /api/Service", Any, None, Fixed("/api/Service")),
        new("GET /api/Service/{id}", Any, None,
            async api => ($"/api/Service/{await api.CreateServiceAsync()}", null)),
        new("POST /api/Service", AdminOnly, None,
            _ => Task.FromResult<(string, object?)>(("/api/Service", new { name = Unique("test-service") }))),
        new("PUT /api/Service/{id}", AdminOnly, None,
            async api => ($"/api/Service/{await api.CreateServiceAsync()}", new { name = Unique("test-service") })),
        new("DELETE /api/Service/{id}", AdminOnly, None,
            async api => ($"/api/Service/{await api.CreateServiceAsync()}", null)),

        // ---- Departments ----
        new("GET /api/Department", Any, None, Fixed("/api/Department")),
        new("GET /api/Department/{id}", Any, None,
            async api => ($"/api/Department/{await api.CreateDepartmentAsync()}", null)),
        new("POST /api/Department", AdminOnly, None,
            _ => Task.FromResult<(string, object?)>(("/api/Department", new DepartmentDto { Name = Unique("test-department") })),
            HttpStatusCode.Created),
        new("PUT /api/Department/{id}", AdminOnly, None,
            async api => ($"/api/Department/{await api.CreateDepartmentAsync()}", new DepartmentDto { Name = Unique("test-department") })),
        new("DELETE /api/Department/{id}", AdminOnly, None,
            async api => ($"/api/Department/{await api.CreateDepartmentAsync()}", null),
            HttpStatusCode.NoContent),

        // ---- Doctors ----
        new("GET /api/Doctor", Any, None, Fixed("/api/Doctor")),
        new("GET /api/Doctor/{id}", Any, None,
            async api => ($"/api/Doctor/{await api.CreateDoctorAsync()}", null)),
        new("POST /api/Doctor", AdminOnly, None,
            _ => Task.FromResult<(string, object?)>(("/api/Doctor", NewDoctorBody()))),
        new("PUT /api/Doctor/{id}", AdminOnly, None,
            async api => ($"/api/Doctor/{await api.CreateDoctorAsync()}", NewDoctorBody())),
        new("DELETE /api/Doctor/{id}", AdminOnly, None,
            async api => ($"/api/Doctor/{await api.CreateDoctorAsync()}", null)),

        // ---- Appointments ----
        new("GET /api/Appointment", Staff, PatientOwnOnly, Fixed("/api/Appointment")),
        new("GET /api/Appointment/{id}", Staff, PatientOwnOnly,
            async api => ($"/api/Appointment/{(await api.CreateAppointmentAsync()).Id}", null)),
        new("POST /api/Appointment", Staff, PatientOwnOnly,
            async api => ("/api/Appointment", new AppointmentCreateDto
            {
                PatientId = (await api.CreatePatientAsync()).Id,
                DoctorId = await api.CreateDoctorAsync(),
                ScheduledAt = DateTime.UtcNow.AddDays(30),
                DurationMinutes = 30,
                Reason = Unique("synthetic-reason")
            }),
            HttpStatusCode.Created),
        new("PUT /api/Appointment/{id}", Staff, None,
            async api =>
            {
                var appointment = await api.CreateAppointmentAsync();
                return ($"/api/Appointment/{appointment.Id}", new AppointmentUpdateDto
                {
                    DoctorId = appointment.DoctorId,
                    ScheduledAt = DateTime.UtcNow.AddDays(31),
                    DurationMinutes = 45,
                    Reason = Unique("synthetic-reason")
                });
            }),
        new("POST /api/Appointment/{id}/cancel", Staff, PatientOwnOnly,
            async api => ($"/api/Appointment/{(await api.CreateAppointmentAsync()).Id}/cancel", null)),
        new("DELETE /api/Appointment/{id}", AdminOnly, None,
            async api => ($"/api/Appointment/{(await api.CreateAppointmentAsync()).Id}", null),
            HttpStatusCode.NoContent),

        // ---- Medical records: admins have no access to clinical content ----
        new("GET /api/MedicalRecord", ClinicalStaff, PatientOwnOnly, Fixed("/api/MedicalRecord")),
        new("GET /api/MedicalRecord/{id}", ClinicalStaff, PatientOwnOnly,
            async api => ($"/api/MedicalRecord/{(await api.CreateMedicalRecordAsync()).Id}", null)),
        new("POST /api/MedicalRecord", DoctorOnly, None,
            async api => ("/api/MedicalRecord", new MedicalRecordCreateDto
            {
                PatientId = (await api.CreatePatientAsync()).Id,
                DoctorId = await api.CreateDoctorAsync(),
                Diagnosis = Unique("synthetic-diagnosis")
            }),
            HttpStatusCode.Created),
        new("PUT /api/MedicalRecord/{id}", DoctorOnly, None,
            async api => ($"/api/MedicalRecord/{(await api.CreateMedicalRecordAsync()).Id}",
                new MedicalRecordUpdateDto { Diagnosis = Unique("synthetic-diagnosis") })),

        // ---- Audit log ----
        new("GET /api/AuditLog", AdminOnly, None, Fixed("/api/AuditLog")),
    };

    /// <summary>
    /// Controllers whose endpoints have no matrix rows yet. This is recorded debt against gate
    /// G-SEC-01: remove a name here only when rows for all of that controller's endpoints exist.
    /// Endpoints of any controller not listed here must be in <see cref="Rows"/>.
    /// </summary>
    public static readonly string[] ControllersNotYetCovered = Array.Empty<string>();

    public static IEnumerable<object[]> Cells() =>
        from row in Rows
        from column in Columns
        where !row.ObjectLevel.Contains(column)
        select new object[] { row.Endpoint, column };
}
