using Data;
using Domain.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Services.AuditService;
using Services.Common;

namespace HospitalManagement.UnitTests.Support;

/// <summary>
/// An in-memory database per test. Explicit transactions are accepted and ignored (the in-memory provider has
/// none); behavior that depends on real transactions or SQL is tested in the API tests (TEST_STRATEGY §2).
/// </summary>
public static class TestDb
{
    public static ApplicationDbContext Create() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("unit-" + Guid.NewGuid())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public static Service AddService(this ApplicationDbContext db, string name = "Synthetic service")
    {
        var service = new Service { Name = name };
        db.Services.Add(service);
        db.SaveChanges();
        return service;
    }

    public static Patient AddPatient(this ApplicationDbContext db, int serviceId, string name = "Test Patient")
    {
        var patient = new Patient { UserName = name, Email = "patient@hospital.test", Phone = 10000000, ServiceId = serviceId };
        db.Patients.Add(patient);
        db.SaveChanges();
        return patient;
    }

    public static Doctor AddDoctor(this ApplicationDbContext db, string lastName = "Doctor")
    {
        var doctor = new Doctor { FirstName = "Test", LastName = lastName, DoctorServices = new List<Domain.Entities.DoctorService>() };
        db.Doctors.Add(doctor);
        db.SaveChanges();
        return doctor;
    }

    public static User AddUser(this ApplicationDbContext db, string subject, string userName, bool isActive = true)
    {
        var user = new User { KeycloakId = subject, UserName = userName, IsActive = isActive, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }
}

/// <summary>The caller as the token describes it; every field can be set by the test.</summary>
public sealed class FakeCaller : ICurrentUserContext
{
    public bool IsAuthenticated => Subject != null;
    public string? Subject { get; set; } = "sub-caller";
    public string? UserName { get; set; } = "caller.user";
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? SessionId { get; set; } = "session-1";
    public string? IpAddress => null;
    public HashSet<string> RoleSet { get; } = new() { Domain.Constants.Roles.Admin };
    public IReadOnlyCollection<string> Roles => RoleSet;
    public bool IsInRole(string role) => RoleSet.Contains(role);

    public FakeCaller As(string role)
    {
        RoleSet.Clear();
        RoleSet.Add(role);
        return this;
    }
}

/// <summary>Records every audit call instead of writing it.</summary>
public sealed class AuditSpy : IAuditService
{
    public List<(string Action, string? EntityType, string? EntityId, string Details)> Entries { get; } = new();

    public Task LogAsync(string action, string? entityType = null, object? entityId = null, object? details = null,
        bool success = true, CancellationToken cancellationToken = default)
    {
        Entries.Add((action, entityType, entityId?.ToString(),
            details == null ? "" : System.Text.Json.JsonSerializer.Serialize(details)));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Domain.Dtos.AuditLogDto>> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Domain.Dtos.AuditLogDto>>(Array.Empty<Domain.Dtos.AuditLogDto>());

    public IEnumerable<string> Actions => Entries.Select(e => e.Action);
}
