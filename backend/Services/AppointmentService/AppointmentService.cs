using Data;
using Domain.Constants;
using Domain.Dtos;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditService;
using Services.Common;
using Services.Integrations;

namespace Services.AppointmentService
{
    public class AppointmentService : IAppointmentService
    {
        private const int MaxDurationMinutes = 480;

        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserContext _currentUser;
        private readonly IAuditService _audit;
        private readonly INotificationService _notifications;
        private readonly TimeProvider _time;

        public AppointmentService(ApplicationDbContext context, ICurrentUserContext currentUser,
            IAuditService audit, INotificationService notifications, TimeProvider time)
        {
            _context = context;
            _currentUser = currentUser;
            _audit = audit;
            _notifications = notifications;
            _time = time;
        }

        /// <summary>The current time; from <see cref="TimeProvider"/> so tests can fix it.</summary>
        private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

        public async Task<IReadOnlyList<AppointmentDto>> GetAllAsync(int? patientId = null, int? doctorId = null,
            DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Appointments.AsNoTracking();
            if (patientId.HasValue) query = query.Where(a => a.PatientId == patientId.Value);
            if (doctorId.HasValue) query = query.Where(a => a.DoctorId == doctorId.Value);
            if (from.HasValue) query = query.Where(a => a.ScheduledAt >= from.Value);
            if (to.HasValue) query = query.Where(a => a.ScheduledAt <= to.Value);

            return await Project(query.OrderBy(a => a.ScheduledAt)).ToListAsync(cancellationToken);
        }

        public async Task<AppointmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            await Project(_context.Appointments.AsNoTracking().Where(a => a.Id == id)).FirstOrDefaultAsync(cancellationToken);

        public async Task<ServiceResult<AppointmentDto>> CreateAsync(AppointmentCreateDto dto, CancellationToken cancellationToken = default)
        {
            if (dto.ScheduledAt.Kind == DateTimeKind.Unspecified) return MissingTimeZone();
            var scheduledAt = dto.ScheduledAt.ToUniversalTime();
            if (scheduledAt <= UtcNow)
                return ServiceResult<AppointmentDto>.Invalid("An appointment must be scheduled in the future");
            if (!await _context.Patients.AnyAsync(p => p.Id == dto.PatientId, cancellationToken))
                return ServiceResult<AppointmentDto>.Invalid($"Patient with ID {dto.PatientId} not found");

            var error = await ValidateSlotAsync(null, dto.DoctorId, dto.ServiceId, scheduledAt, dto.DurationMinutes, cancellationToken);
            if (error != null) return error;

            var appointment = new Appointment
            {
                PatientId = dto.PatientId,
                DoctorId = dto.DoctorId,
                ServiceId = dto.ServiceId,
                ScheduledAt = scheduledAt,
                DurationMinutes = dto.DurationMinutes,
                Status = AppointmentStatus.Scheduled,
                Reason = dto.Reason,
                CreatedAt = UtcNow,
                CreatedBy = _currentUser.Subject
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(AuditActions.AppointmentCreated, nameof(Appointment), appointment.Id,
                new { appointment.PatientId, appointment.DoctorId, appointment.ScheduledAt }, cancellationToken: cancellationToken);
            await _notifications.NotifyAppointmentScheduledAsync(appointment, cancellationToken);

            return ServiceResult<AppointmentDto>.Ok((await GetByIdAsync(appointment.Id, cancellationToken))!);
        }

        public async Task<ServiceResult<AppointmentDto>> UpdateAsync(int id, AppointmentUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var appointment = await _context.Appointments.FindAsync(new object[] { id }, cancellationToken);
            if (appointment == null) return ServiceResult<AppointmentDto>.NotFound($"Appointment with ID {id} not found");

            if (dto.ScheduledAt.Kind == DateTimeKind.Unspecified) return MissingTimeZone();
            var scheduledAt = dto.ScheduledAt.ToUniversalTime();
            var now = UtcNow;
            var rescheduled = appointment.DoctorId != dto.DoctorId
                || appointment.ServiceId != dto.ServiceId
                || appointment.ScheduledAt != scheduledAt
                || appointment.DurationMinutes != dto.DurationMinutes;
            var before = new { appointment.DoctorId, appointment.ScheduledAt, Status = appointment.Status.ToString() };

            // Completed, NoShow and Cancelled are history: only the notes can still be added to.
            if (IsFinal(appointment.Status))
            {
                if (rescheduled || dto.Status != appointment.Status || dto.Reason != appointment.Reason)
                    return ServiceResult<AppointmentDto>.Conflict(
                        $"A {appointment.Status} appointment cannot be changed; only its notes can be updated");

                appointment.Notes = dto.Notes;
                return await SaveUpdateAsync(appointment, before, cancellationToken);
            }

            // From here the appointment is Scheduled or Confirmed.
            var transitionError = ValidateTransition(appointment, dto.Status, rescheduled, now);
            if (transitionError != null) return transitionError;

            // A documented visit cannot become NoShow (R-053), the same rule as CorrectStatusAsync.
            if (dto.Status == AppointmentStatus.NoShow
                && await _context.MedicalRecords.AnyAsync(m => m.AppointmentId == id, cancellationToken))
                return ServiceResult<AppointmentDto>.Conflict(
                    "The appointment has a medical record; a documented visit cannot be marked as NoShow");

            if (rescheduled)
            {
                // A visit that has started is closed with Completed or NoShow, never moved: moving it back into
                // the future would let it be cancelled, even with a medical record attached (R-053).
                if (appointment.ScheduledAt <= now)
                    return ServiceResult<AppointmentDto>.Conflict(
                        "An appointment that has started cannot be moved; set it to Completed or NoShow");
                if (scheduledAt <= now)
                    return ServiceResult<AppointmentDto>.Invalid("An appointment must be scheduled in the future");
                var slotError = await ValidateSlotAsync(id, dto.DoctorId, dto.ServiceId, scheduledAt, dto.DurationMinutes, cancellationToken);
                if (slotError != null) return slotError;
            }

            appointment.DoctorId = dto.DoctorId;
            appointment.ServiceId = dto.ServiceId;
            appointment.ScheduledAt = scheduledAt;
            appointment.DurationMinutes = dto.DurationMinutes;
            // A moved appointment has to be confirmed again.
            appointment.Status = rescheduled ? AppointmentStatus.Scheduled : dto.Status;
            appointment.Reason = dto.Reason;
            appointment.Notes = dto.Notes;

            return await SaveUpdateAsync(appointment, before, cancellationToken);
        }

        /// <summary>
        /// Allowed status changes for a Scheduled or Confirmed appointment:
        /// Scheduled ⇄ Confirmed at any time; Cancelled only before the start;
        /// Completed (doctor only) and NoShow only once the appointment has started.
        /// A change of time, doctor, service or duration cannot be combined with a final status.
        /// </summary>
        private ServiceResult<AppointmentDto>? ValidateTransition(Appointment appointment, AppointmentStatus target,
            bool rescheduled, DateTime now)
        {
            if (target is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed) return null;

            if (rescheduled)
                return ServiceResult<AppointmentDto>.Conflict(
                    $"An appointment cannot be moved and set to {target} in the same request");

            var started = appointment.ScheduledAt <= now;
            return target switch
            {
                AppointmentStatus.Cancelled when started => ServiceResult<AppointmentDto>.Conflict(
                    "An appointment that has started cannot be cancelled; set it to Completed or NoShow"),
                AppointmentStatus.Completed when !_currentUser.IsInRole(Roles.Doctor) =>
                    ServiceResult<AppointmentDto>.Forbidden("Only a doctor can mark an appointment as Completed"),
                AppointmentStatus.Completed or AppointmentStatus.NoShow when !started =>
                    ServiceResult<AppointmentDto>.Conflict($"An appointment cannot be set to {target} before it starts"),
                _ => null
            };
        }

        public async Task<ServiceResult<AppointmentDto>> CorrectStatusAsync(int id, AppointmentStatusCorrectionDto dto,
            CancellationToken cancellationToken = default)
        {
            var appointment = await _context.Appointments.FindAsync(new object[] { id }, cancellationToken);
            if (appointment == null) return ServiceResult<AppointmentDto>.NotFound($"Appointment with ID {id} not found");
            if (!IsFinal(appointment.Status))
                return ServiceResult<AppointmentDto>.Conflict(
                    $"A {appointment.Status} appointment is not final; change it with PUT /api/Appointment/{{id}}");

            var from = appointment.Status;
            var allowed = (from, dto.Status) switch
            {
                (AppointmentStatus.NoShow, AppointmentStatus.Completed) => true,
                (AppointmentStatus.Completed, AppointmentStatus.NoShow) => true,
                (AppointmentStatus.Cancelled, AppointmentStatus.Scheduled) => true,
                _ => false
            };
            if (!allowed)
                return ServiceResult<AppointmentDto>.Conflict(
                    $"A {from} appointment cannot be corrected to {dto.Status}");

            // A documented visit took place: it cannot become a no-show while a medical record belongs to it (R-053).
            if (dto.Status == AppointmentStatus.NoShow
                && await _context.MedicalRecords.AnyAsync(m => m.AppointmentId == id, cancellationToken))
                return ServiceResult<AppointmentDto>.Conflict(
                    "The appointment has a medical record; a documented visit cannot be corrected to NoShow");

            if (dto.Status == AppointmentStatus.Scheduled)
            {
                // Reopening a cancelled appointment is only possible while it can still take place.
                if (appointment.ScheduledAt <= UtcNow)
                    return ServiceResult<AppointmentDto>.Conflict("A cancelled appointment that has already started cannot be reopened");
                var slotError = await ValidateSlotAsync(id, appointment.DoctorId, appointment.ServiceId,
                    appointment.ScheduledAt, appointment.DurationMinutes, cancellationToken);
                if (slotError != null) return slotError;
            }

            appointment.Status = dto.Status;
            appointment.UpdatedAt = UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(AuditActions.AppointmentStatusCorrected, nameof(Appointment), appointment.Id,
                new { From = from.ToString(), To = dto.Status.ToString(), dto.Reason }, cancellationToken: cancellationToken);
            await _notifications.NotifyAppointmentUpdatedAsync(appointment, cancellationToken);

            return ServiceResult<AppointmentDto>.Ok((await GetByIdAsync(appointment.Id, cancellationToken))!);
        }

        private static bool IsFinal(AppointmentStatus status) =>
            status is AppointmentStatus.Completed or AppointmentStatus.NoShow or AppointmentStatus.Cancelled;

        public async Task<ServiceResult<AppointmentDto>> CancelAsync(int id, CancellationToken cancellationToken = default)
        {
            var appointment = await _context.Appointments.FindAsync(new object[] { id }, cancellationToken);
            if (appointment == null) return ServiceResult<AppointmentDto>.NotFound($"Appointment with ID {id} not found");
            if (IsFinal(appointment.Status))
                return ServiceResult<AppointmentDto>.Conflict($"A {appointment.Status} appointment cannot be cancelled");
            if (appointment.ScheduledAt <= UtcNow)
                return ServiceResult<AppointmentDto>.Conflict(
                    "An appointment that has started cannot be cancelled; set it to Completed or NoShow");

            var before = new { appointment.DoctorId, appointment.ScheduledAt, Status = appointment.Status.ToString() };
            appointment.Status = AppointmentStatus.Cancelled;

            return await SaveUpdateAsync(appointment, before, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var appointment = await _context.Appointments.FindAsync(new object[] { id }, cancellationToken);
            if (appointment == null) return false;

            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(AuditActions.AppointmentDeleted, nameof(Appointment), id,
                new { appointment.PatientId, appointment.DoctorId, appointment.ScheduledAt }, cancellationToken: cancellationToken);
            return true;
        }

        private async Task<ServiceResult<AppointmentDto>> SaveUpdateAsync(Appointment appointment, object before, CancellationToken cancellationToken)
        {
            appointment.UpdatedAt = UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(AuditActions.AppointmentUpdated, nameof(Appointment), appointment.Id,
                new
                {
                    Before = before,
                    After = new { appointment.DoctorId, appointment.ScheduledAt, Status = appointment.Status.ToString() }
                }, cancellationToken: cancellationToken);
            await _notifications.NotifyAppointmentUpdatedAsync(appointment, cancellationToken);

            return ServiceResult<AppointmentDto>.Ok((await GetByIdAsync(appointment.Id, cancellationToken))!);
        }

        /// <summary>Checks the doctor and service exist and the doctor is free for the whole slot.</summary>
        private async Task<ServiceResult<AppointmentDto>?> ValidateSlotAsync(int? appointmentId, int doctorId, int? serviceId,
            DateTime start, int durationMinutes, CancellationToken cancellationToken)
        {
            if (!await _context.Doctors.AnyAsync(d => d.Id == doctorId, cancellationToken))
                return ServiceResult<AppointmentDto>.Invalid($"Doctor with ID {doctorId} not found");
            if (serviceId.HasValue && !await _context.Services.AnyAsync(s => s.Id == serviceId.Value, cancellationToken))
                return ServiceResult<AppointmentDto>.Invalid($"Service with ID {serviceId} not found");

            var end = start.AddMinutes(durationMinutes);
            var windowStart = start.AddMinutes(-MaxDurationMinutes);
            var candidates = await _context.Appointments.AsNoTracking()
                .Where(a => a.DoctorId == doctorId
                    && a.Id != appointmentId
                    && a.Status != AppointmentStatus.Cancelled
                    && a.ScheduledAt > windowStart
                    && a.ScheduledAt < end)
                .Select(a => new { a.ScheduledAt, a.DurationMinutes })
                .ToListAsync(cancellationToken);

            return candidates.Any(a => a.ScheduledAt.AddMinutes(a.DurationMinutes) > start)
                ? ServiceResult<AppointmentDto>.Conflict("The doctor already has an appointment in that time slot")
                : null;
        }

        /// <summary>
        /// A time without a time zone ("2027-03-01T09:00:00") is ambiguous: read as UTC it shifts the appointment
        /// by the clinic's offset, and by one more hour across a daylight-saving change (R-051). It is refused;
        /// the client sends "Z" or an offset such as "+01:00", and the time is stored in UTC.
        /// </summary>
        private static ServiceResult<AppointmentDto> MissingTimeZone() => ServiceResult<AppointmentDto>.Invalid(
            "scheduledAt must include a time zone: end it with Z (UTC) or an offset such as +01:00");

        private static IQueryable<AppointmentDto> Project(IQueryable<Appointment> query) =>
            query.Select(a => new AppointmentDto
            {
                Id = a.Id,
                PatientId = a.PatientId,
                PatientName = a.Patient!.UserName,
                DoctorId = a.DoctorId,
                DoctorName = a.Doctor!.FirstName + " " + a.Doctor.LastName,
                ServiceId = a.ServiceId,
                ServiceName = a.Service!.Name,
                ScheduledAt = a.ScheduledAt,
                DurationMinutes = a.DurationMinutes,
                Status = a.Status,
                Reason = a.Reason,
                Notes = a.Notes,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            });
    }
}
