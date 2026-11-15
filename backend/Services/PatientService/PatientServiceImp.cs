using Data;
using Domain.Constants;
using Domain.Dtos;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditService;
using Services.Common;

namespace Services.PatientService
{
    public class PatientServiceImp : IPatientService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;

        public PatientServiceImp(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IReadOnlyList<PatientDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
            await Project(_context.Patients.AsNoTracking().OrderBy(p => p.UserName)).ToListAsync(cancellationToken);

        public async Task<PatientDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            await Project(_context.Patients.AsNoTracking().Where(p => p.Id == id)).FirstOrDefaultAsync(cancellationToken);

        public async Task<ServiceResult<PatientDto>> CreateAsync(PatientCreationDto dto, CancellationToken cancellationToken = default)
        {
            if (!await _context.Services.AnyAsync(s => s.Id == dto.ServiceId, cancellationToken))
                return ServiceResult<PatientDto>.Invalid($"Service with ID {dto.ServiceId} not found");

            var patient = new Patient();
            Apply(patient, dto);
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync(cancellationToken);

            // Ids only: the audit log never holds personal data.
            await _audit.LogAsync(AuditActions.PatientCreated, nameof(Patient), patient.Id,
                new { patient.ServiceId }, cancellationToken: cancellationToken);
            return ServiceResult<PatientDto>.Ok((await GetByIdAsync(patient.Id, cancellationToken))!);
        }

        public async Task<ServiceResult<PatientDto>> UpdateAsync(int id, PatientCreationDto dto, CancellationToken cancellationToken = default)
        {
            var patient = await _context.Patients.FindAsync(new object[] { id }, cancellationToken);
            if (patient == null) return ServiceResult<PatientDto>.NotFound($"Patient with ID {id} not found");
            if (!await _context.Services.AnyAsync(s => s.Id == dto.ServiceId, cancellationToken))
                return ServiceResult<PatientDto>.Invalid($"Service with ID {dto.ServiceId} not found");

            var changedFields = ChangedFields(patient, dto);
            Apply(patient, dto);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(AuditActions.PatientUpdated, nameof(Patient), patient.Id,
                new { ChangedFields = changedFields }, cancellationToken: cancellationToken);
            return ServiceResult<PatientDto>.Ok((await GetByIdAsync(id, cancellationToken))!);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var patient = await _context.Patients.FindAsync(new object[] { id }, cancellationToken);
            if (patient == null) return false;

            _context.Patients.Remove(patient);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(AuditActions.PatientDeleted, nameof(Patient), id, cancellationToken: cancellationToken);
            return true;
        }

        private static void Apply(Patient patient, PatientCreationDto dto)
        {
            patient.UserName = dto.UserName;
            patient.Phone = dto.Phone;
            patient.Email = dto.Email;
            patient.ServiceId = dto.ServiceId;
        }

        private static string[] ChangedFields(Patient patient, PatientCreationDto dto)
        {
            var fields = new List<string>();
            if (patient.UserName != dto.UserName) fields.Add(nameof(Patient.UserName));
            if (patient.Phone != dto.Phone) fields.Add(nameof(Patient.Phone));
            if (patient.Email != dto.Email) fields.Add(nameof(Patient.Email));
            if (patient.ServiceId != dto.ServiceId) fields.Add(nameof(Patient.ServiceId));
            return fields.ToArray();
        }

        private static IQueryable<PatientDto> Project(IQueryable<Patient> query) =>
            query.Select(p => new PatientDto
            {
                Id = p.Id,
                UserName = p.UserName,
                Phone = p.Phone,
                Email = p.Email,
                ServiceId = p.ServiceId,
                ServiceName = p.Service!.Name
            });
    }
}
