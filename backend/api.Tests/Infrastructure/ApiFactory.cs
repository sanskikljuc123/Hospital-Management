using System.Net.Http.Headers;
using System.Net.Http.Json;
using Data;
using Domain.Dtos;
using Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Testcontainers.PostgreSql;

namespace api.Tests.Infrastructure;

/// <summary>
/// Runs the real API in-process against a real PostgreSQL instance created for the test run,
/// with the real migrations applied and real token validation. Nothing in the request pipeline
/// is replaced: only the database address and the trusted signing key are test-specific.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same engine and major version as docker-compose.yml.
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public TestTokenIssuer Tokens { get; } = new();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        _ = Server; // start the API now so migration failures surface here, not in the first test
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": tests see production error handling and no Swagger.
        builder.UseEnvironment("Testing");

        // UseSetting (not ConfigureAppConfiguration) because Program.cs reads these values
        // before the host is built.
        builder.UseSetting("ConnectionStrings:hospitaldb", _database.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Keycloak:Authority", TestTokenIssuer.Issuer);
        builder.UseSetting("Keycloak:Issuer", TestTokenIssuer.Issuer);
        builder.UseSetting("Keycloak:Audience", TestTokenIssuer.Audience);

        builder.ConfigureTestServices(services =>
        {
            // Supply the issuer's public key directly instead of downloading it from the
            // identity provider's metadata endpoint. Validation parameters stay as configured
            // by the API (issuer, audience, lifetime, signature).
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = TestTokenIssuer.Issuer };
                configuration.SigningKeys.Add(Tokens.SigningKey);
                options.Configuration = configuration;
            });
        });
    }

    public HttpClient NewClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>Sends a request as the given identity; <c>null</c> sends no Authorization header.</summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, TestIdentity? identity, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (identity != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Tokens.Issue(identity));
        if (body != null)
            request.Content = JsonContent.Create(body);
        return await NewClient().SendAsync(request);
    }

    public async Task<HttpResponseMessage> SendWithTokenAsync(HttpMethod method, string url, string token)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await NewClient().SendAsync(request);
    }

    // ---- Fixtures: each test creates the data it needs, uniquely keyed. ----

    public async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public Task<int> CreateServiceAsync() => WithDbAsync(async db =>
    {
        var service = new Service { Name = $"test-service-{Guid.NewGuid():N}" };
        db.Services.Add(service);
        await db.SaveChangesAsync();
        return service.Id;
    });

    public async Task<Patient> CreatePatientAsync(int? serviceId = null)
    {
        var id = serviceId ?? await CreateServiceAsync();
        var marker = Guid.NewGuid().ToString("N");
        return await WithDbAsync(async db =>
        {
            var patient = new Patient
            {
                UserName = $"synthetic-patient-{marker}",
                Email = $"patient-{marker}@example.test",
                Phone = 5550100,
                ServiceId = id
            };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();
            return patient;
        });
    }

    public Task<Patient?> ReadPatientAsync(int id) =>
        WithDbAsync(async db => await db.Patients.FindAsync(id));

    public Task<int> CreateDepartmentAsync() => WithDbAsync(async db =>
    {
        var department = new Department { Name = $"test-department-{Guid.NewGuid():N}" };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        return department.Id;
    });

    public Task<int> CreateDoctorAsync() => WithDbAsync(async db =>
    {
        var doctor = new Doctor
        {
            FirstName = "Synthetic",
            LastName = $"doctor-{Guid.NewGuid():N}",
            DoctorServices = new List<DoctorService>()
        };
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();
        return doctor.Id;
    });

    /// <summary>A scheduled appointment in the future, with its own doctor so slots never collide.</summary>
    public async Task<Appointment> CreateAppointmentAsync(int? patientId = null)
    {
        var patient = patientId ?? (await CreatePatientAsync()).Id;
        var doctor = await CreateDoctorAsync();
        return await WithDbAsync(async db =>
        {
            var appointment = new Appointment
            {
                PatientId = patient,
                DoctorId = doctor,
                ScheduledAt = DateTime.UtcNow.AddDays(30),
                DurationMinutes = 30,
                Reason = $"synthetic-reason-{Guid.NewGuid():N}",
                CreatedAt = DateTime.UtcNow
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            return appointment;
        });
    }

    public Task<Appointment?> ReadAppointmentAsync(int id) =>
        WithDbAsync(async db => await db.Appointments.FindAsync(id));

    public async Task<MedicalRecord> CreateMedicalRecordAsync(int? patientId = null)
    {
        var patient = patientId ?? (await CreatePatientAsync()).Id;
        var doctor = await CreateDoctorAsync();
        return await WithDbAsync(async db =>
        {
            var record = new MedicalRecord
            {
                PatientId = patient,
                DoctorId = doctor,
                Diagnosis = $"synthetic-diagnosis-{Guid.NewGuid():N}",
                Treatment = $"synthetic-treatment-{Guid.NewGuid():N}",
                CreatedAt = DateTime.UtcNow
            };
            db.MedicalRecords.Add(record);
            await db.SaveChangesAsync();
            return record;
        });
    }

    public Task<MedicalRecord?> ReadMedicalRecordAsync(int id) =>
        WithDbAsync(async db => await db.MedicalRecords.FindAsync(id));

    /// <summary>Makes sure the identity has a local profile (as its first request would) and returns it.</summary>
    public async Task<UserDto> ProvisionAsync(TestIdentity identity)
    {
        var response = await SendAsync(HttpMethod.Get, "/api/User/me", identity);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    /// <summary>Links an identity to a patient record through the real admin endpoint.</summary>
    public async Task LinkToPatientAsync(TestIdentity identity, int patientId)
    {
        var user = await ProvisionAsync(identity);
        var response = await SendAsync(HttpMethod.Put, $"/api/User/{user.Id}/link",
            TestIdentities.Admin1, new UserLinkDto { PatientId = patientId });
        response.EnsureSuccessStatusCode();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
