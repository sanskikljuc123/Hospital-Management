using Data;
using Domain.Constants;
using Domain.Dtos;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditService;
using Services.Common;

namespace Services.MedicalRecordService
{
    public class MedicalRecordService : IMedicalRecordService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly ICurrentUserContext _currentUser;
        private readonly TimeProvider _time;

        public MedicalRecordService(ApplicationDbContext context, IAuditService audit, ICurrentUserContext currentUser,
            TimeProvider time)
        {
            _context = context;
            _audit = audit;
            _currentUser = currentUser;
            _time = time;
        }

        public async Task<IReadOnlyList<MedicalRecordDto>> GetAllAsync(int? patientId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.MedicalRecords.AsNoTracking();
            if (patientId.HasValue) query = query.Where(m => m.PatientId == patientId.Value);
            return await Project(query.OrderByDescending(m => m.CreatedAt)).ToListAsync(cancellationToken);
        }

        public async Task<MedicalRecordDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            await Project(_context.MedicalRecords.AsNoTracking().Where(m => m.Id == id)).FirstOrDefaultAsync(cancellationToken);

        public async Task<IReadOnlyList<MedicalRecordRevisionDto>?> GetHistoryAsync(int id, CancellationToken cancellationToken = default)
        {
            if (!await _context.MedicalRecords.AnyAsync(m => m.Id == id, cancellationToken)) return null;

            var revisions = await _context.MedicalRecordRevisions.AsNoTracking()
                .Where(r => r.MedicalRecordId == id)
                .OrderBy(r => r.Revision)
                .Select(r => new
                {
                    r.Revision, r.Diagnosis, r.Treatment, r.Notes, r.ChangedFields, r.EditedByDoctorId,
                    EditedByDoctorName = r.EditedByDoctor!.FirstName + " " + r.EditedByDoctor.LastName,
                    r.EditedByUserName, r.EditedAt
                })
                .ToListAsync(cancellationToken);

            return revisions.Select(r => new MedicalRecordRevisionDto
            {
                Revision = r.Revision,
                Diagnosis = r.Diagnosis,
                Treatment = r.Treatment,
                Notes = r.Notes,
                ChangedFields = r.ChangedFields.Length == 0 ? Array.Empty<string>() : r.ChangedFields.Split(','),
                EditedByDoctorId = r.EditedByDoctorId,
                EditedByDoctorName = r.EditedByDoctorName,
                EditedByUserName = r.EditedByUserName,
                EditedAt = r.EditedAt
            }).ToList();
        }

        public async Task<ServiceResult<MedicalRecordDto>> CreateAsync(MedicalRecordCreateDto dto, int doctorId, CancellationToken cancellationToken = default)
        {
            if (!await _context.Patients.AnyAsync(p => p.Id == dto.PatientId, cancellationToken))
                return ServiceResult<MedicalRecordDto>.Invalid($"Patient with ID {dto.PatientId} not found");
            var now = _time.GetUtcNow().UtcDateTime;
            if (dto.AppointmentId.HasValue)
            {
                // The appointment's row is locked first, so a status change at the same moment waits for this
                // record, or this record sees that change (A-006, AppointmentLocking).
                var appointment = await _context.FindAppointmentForUpdateAsync(dto.AppointmentId.Value, cancellationToken);

                // A record can only be attached to an appointment of this patient with this doctor (400)...
                if (appointment == null || appointment.PatientId != dto.PatientId || appointment.DoctorId != doctorId)
                    return ServiceResult<MedicalRecordDto>.Invalid(
                        $"Appointment with ID {dto.AppointmentId} not found for this patient and doctor");

                // ...and only to a visit that can have taken place: started, not cancelled, not missed (R-053, 409).
                if (appointment.ScheduledAt > now
                    || appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
                    return ServiceResult<MedicalRecordDto>.Conflict(
                        "A medical record can only be attached to an appointment that has started and was not cancelled or missed");
            }

            var record = new MedicalRecord
            {
                PatientId = dto.PatientId,
                DoctorId = doctorId,
                AppointmentId = dto.AppointmentId,
                Diagnosis = dto.Diagnosis,
                Treatment = dto.Treatment,
                Notes = dto.Notes,
                CreatedAt = now
            };
            // Revision 1 is the record as created; it is saved in the same transaction as the record.
            record.Revisions.Add(NewRevision(1, record, Array.Empty<string>(), doctorId, now));
            _context.MedicalRecords.Add(record);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(AuditActions.MedicalRecordCreated, nameof(MedicalRecord), record.Id,
                new { record.PatientId, record.DoctorId }, cancellationToken: cancellationToken);

            return ServiceResult<MedicalRecordDto>.Ok((await GetByIdAsync(record.Id, cancellationToken))!);
        }

        public async Task<ServiceResult<MedicalRecordDto>> UpdateAsync(int id, MedicalRecordUpdateDto dto, int doctorId,
            CancellationToken cancellationToken = default)
        {
            var record = await _context.MedicalRecords.FindAsync(new object[] { id }, cancellationToken);
            if (record == null) return ServiceResult<MedicalRecordDto>.NotFound($"Medical record with ID {id} not found");

            var changedFields = new List<string>();
            if (record.Diagnosis != dto.Diagnosis) changedFields.Add(nameof(record.Diagnosis));
            if (record.Treatment != dto.Treatment) changedFields.Add(nameof(record.Treatment));
            if (record.Notes != dto.Notes) changedFields.Add(nameof(record.Notes));
            // Nothing changed: no new revision, no audit entry.
            if (changedFields.Count == 0) return ServiceResult<MedicalRecordDto>.Ok((await GetByIdAsync(id, cancellationToken))!);

            var now = _time.GetUtcNow().UtcDateTime;
            record.Diagnosis = dto.Diagnosis;
            record.Treatment = dto.Treatment;
            record.Notes = dto.Notes;
            record.UpdatedAt = now;
            record.LastEditedByDoctorId = doctorId;

            // The new version is saved in the same transaction as the change, so an edit cannot exist
            // without its trail. A concurrent edit that takes the same number fails on the unique index.
            var lastRevision = await _context.MedicalRecordRevisions
                .Where(r => r.MedicalRecordId == id)
                .MaxAsync(r => (int?)r.Revision, cancellationToken) ?? 0;
            _context.MedicalRecordRevisions.Add(NewRevision(lastRevision + 1, record, changedFields, doctorId, now));
            await _context.SaveChangesAsync(cancellationToken);

            // The audit log records which fields changed, never the clinical content itself.
            await _audit.LogAsync(AuditActions.MedicalRecordUpdated, nameof(MedicalRecord), record.Id,
                new { record.PatientId, Revision = lastRevision + 1, ChangedFields = changedFields, EditedByDoctorId = doctorId },
                cancellationToken: cancellationToken);

            return ServiceResult<MedicalRecordDto>.Ok((await GetByIdAsync(id, cancellationToken))!);
        }

        private MedicalRecordRevision NewRevision(int revision, MedicalRecord record, IEnumerable<string> changedFields,
            int doctorId, DateTime at) => new()
        {
            MedicalRecordId = record.Id,
            Revision = revision,
            Diagnosis = record.Diagnosis,
            Treatment = record.Treatment,
            Notes = record.Notes,
            ChangedFields = string.Join(',', changedFields),
            EditedByDoctorId = doctorId,
            EditedByUserName = _currentUser.UserName,
            EditedAt = at
        };

        private static IQueryable<MedicalRecordDto> Project(IQueryable<MedicalRecord> query) =>
            query.Select(m => new MedicalRecordDto
            {
                Id = m.Id,
                PatientId = m.PatientId,
                PatientName = m.Patient!.UserName,
                DoctorId = m.DoctorId,
                DoctorName = m.Doctor!.FirstName + " " + m.Doctor.LastName,
                AppointmentId = m.AppointmentId,
                Diagnosis = m.Diagnosis,
                Treatment = m.Treatment,
                Notes = m.Notes,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                LastEditedByDoctorId = m.LastEditedByDoctorId,
                LastEditedByDoctorName = m.LastEditedByDoctor == null
                    ? null
                    : m.LastEditedByDoctor.FirstName + " " + m.LastEditedByDoctor.LastName,
                Revision = m.Revisions.Max(r => (int?)r.Revision) ?? 1
            });
    }
}
