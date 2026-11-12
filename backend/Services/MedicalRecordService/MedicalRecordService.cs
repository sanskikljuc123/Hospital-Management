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

        public MedicalRecordService(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IReadOnlyList<MedicalRecordDto>> GetAllAsync(int? patientId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.MedicalRecords.AsNoTracking();
            if (patientId.HasValue) query = query.Where(m => m.PatientId == patientId.Value);
            return await Project(query.OrderByDescending(m => m.CreatedAt)).ToListAsync(cancellationToken);
        }

        public async Task<MedicalRecordDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            await Project(_context.MedicalRecords.AsNoTracking().Where(m => m.Id == id)).FirstOrDefaultAsync(cancellationToken);

        public async Task<ServiceResult<MedicalRecordDto>> CreateAsync(MedicalRecordCreateDto dto, CancellationToken cancellationToken = default)
        {
            if (!await _context.Patients.AnyAsync(p => p.Id == dto.PatientId, cancellationToken))
                return ServiceResult<MedicalRecordDto>.Invalid($"Patient with ID {dto.PatientId} not found");
            if (!await _context.Doctors.AnyAsync(d => d.Id == dto.DoctorId, cancellationToken))
                return ServiceResult<MedicalRecordDto>.Invalid($"Doctor with ID {dto.DoctorId} not found");
            if (dto.AppointmentId.HasValue && !await _context.Appointments.AnyAsync(
                    a => a.Id == dto.AppointmentId.Value && a.PatientId == dto.PatientId, cancellationToken))
                return ServiceResult<MedicalRecordDto>.Invalid($"Appointment with ID {dto.AppointmentId} not found for this patient");

            var record = new MedicalRecord
            {
                PatientId = dto.PatientId,
                DoctorId = dto.DoctorId,
                AppointmentId = dto.AppointmentId,
                Diagnosis = dto.Diagnosis,
                Treatment = dto.Treatment,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow
            };
            _context.MedicalRecords.Add(record);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(AuditActions.MedicalRecordCreated, nameof(MedicalRecord), record.Id,
                new { record.PatientId, record.DoctorId }, cancellationToken: cancellationToken);

            return ServiceResult<MedicalRecordDto>.Ok((await GetByIdAsync(record.Id, cancellationToken))!);
        }

        public async Task<ServiceResult<MedicalRecordDto>> UpdateAsync(int id, MedicalRecordUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var record = await _context.MedicalRecords.FindAsync(new object[] { id }, cancellationToken);
            if (record == null) return ServiceResult<MedicalRecordDto>.NotFound($"Medical record with ID {id} not found");

            // The audit trail records which fields changed, never the clinical content itself.
            var changedFields = new List<string>();
            if (record.Diagnosis != dto.Diagnosis) changedFields.Add(nameof(record.Diagnosis));
            if (record.Treatment != dto.Treatment) changedFields.Add(nameof(record.Treatment));
            if (record.Notes != dto.Notes) changedFields.Add(nameof(record.Notes));

            record.Diagnosis = dto.Diagnosis;
            record.Treatment = dto.Treatment;
            record.Notes = dto.Notes;
            record.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(AuditActions.MedicalRecordUpdated, nameof(MedicalRecord), record.Id,
                new { record.PatientId, ChangedFields = changedFields }, cancellationToken: cancellationToken);

            return ServiceResult<MedicalRecordDto>.Ok((await GetByIdAsync(id, cancellationToken))!);
        }

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
                UpdatedAt = m.UpdatedAt
            });
    }
}
