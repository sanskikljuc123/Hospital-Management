using Data;
using Domain.Constants;
using Domain.Dtos;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Services.AuditService;
using Services.Common;
using Services.Integrations;

namespace HospitalManagement.UnitTests.Appointments;

/// <summary>
/// AppointmentService with an in-memory database, a fixed clock and recording fakes. The rules under test
/// are in the service; the queries are simple enough for the in-memory provider (TEST_STRATEGY §2).
/// </summary>
public sealed class AppointmentTestBed : IDisposable
{
    /// <summary>"Now" for every test: a fixed Monday morning, so rules about the start are exact.</summary>
    public static readonly DateTimeOffset Now = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);

    public FakeTimeProvider Clock { get; } = new(Now);
    public FakeUser User { get; } = new();
    public RecordingAudit Audit { get; } = new();
    public ApplicationDbContext Db { get; }
    public Services.AppointmentService.AppointmentService Service { get; }
    public int PatientId { get; }
    public int DoctorId { get; }
    public int OtherDoctorId { get; }

    public AppointmentTestBed()
    {
        Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("appointments-" + Guid.NewGuid()).Options);

        var service = new Service { Name = "Synthetic service" };
        Db.Services.Add(service);
        Db.SaveChanges();
        var patient = new Patient { UserName = "Test Patient", Email = "patient@hospital.test", ServiceId = service.Id };
        var doctor = new Doctor { FirstName = "Test", LastName = "Doctor A", DoctorServices = new List<DoctorService>() };
        var other = new Doctor { FirstName = "Test", LastName = "Doctor B", DoctorServices = new List<DoctorService>() };
        Db.AddRange(patient, doctor, other);
        Db.SaveChanges();
        (PatientId, DoctorId, OtherDoctorId) = (patient.Id, doctor.Id, other.Id);

        Service = new Services.AppointmentService.AppointmentService(Db, User, Audit, new NoNotifications(), Clock);
    }

    /// <summary>Today's date at the given UTC hour, relative to <see cref="Now"/>.</summary>
    public static DateTime At(int daysFromNow, int hour, int minute = 0) =>
        Now.UtcDateTime.Date.AddDays(daysFromNow).AddHours(hour).AddMinutes(minute);

    public async Task<AppointmentDto> BookAsync(DateTime start, int durationMinutes = 30, int? doctorId = null)
    {
        var result = await Service.CreateAsync(new AppointmentCreateDto
        {
            PatientId = PatientId, DoctorId = doctorId ?? DoctorId, ScheduledAt = start, DurationMinutes = durationMinutes
        });
        Assert.True(result.Success, result.Error);
        return result.Value!;
    }

    public AppointmentUpdateDto UpdateOf(AppointmentDto a, AppointmentStatus status) => new()
    {
        DoctorId = a.DoctorId, ServiceId = a.ServiceId, ScheduledAt = a.ScheduledAt,
        DurationMinutes = a.DurationMinutes, Status = status, Reason = a.Reason, Notes = a.Notes
    };

    /// <summary>Moves the clock past the start of the appointment.</summary>
    public void AdvanceTo(DateTime utc) => Clock.SetUtcNow(new DateTimeOffset(utc, TimeSpan.Zero));

    public void Dispose() => Db.Dispose();

    public sealed class FakeUser : ICurrentUserContext
    {
        public HashSet<string> RoleSet { get; } = new() { Domain.Constants.Roles.Nurse };
        public bool IsAuthenticated => true;
        public string? Subject => "sub-test";
        public string? UserName => "test.user";
        public string? Email => null;
        public string? FirstName => null;
        public string? LastName => null;
        public string? SessionId => "session";
        public string? IpAddress => null;
        public IReadOnlyCollection<string> Roles => RoleSet;
        public bool IsInRole(string role) => RoleSet.Contains(role);

        public void ActAs(string role) { RoleSet.Clear(); RoleSet.Add(role); }
    }

    public sealed class RecordingAudit : IAuditService
    {
        public List<(string Action, object? EntityId, object? Details)> Entries { get; } = new();

        public Task LogAsync(string action, string? entityType = null, object? entityId = null, object? details = null,
            bool success = true, CancellationToken cancellationToken = default)
        {
            Entries.Add((action, entityId, details));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditLogDto>> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditLogDto>>(Array.Empty<AuditLogDto>());
    }

    private sealed class NoNotifications : INotificationService
    {
        public Task NotifyAppointmentScheduledAsync(Appointment appointment, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyAppointmentUpdatedAsync(Appointment appointment, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
