using Data;
using Domain.Constants;
using Domain.Dtos;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditService;
using Services.Common;

namespace Services.DoctorService
{
    public class DoctorServiceImp : IDoctorService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;

        public DoctorServiceImp(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IReadOnlyList<DoctorDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
            await Project(_context.Doctors.AsNoTracking().OrderBy(d => d.LastName).ThenBy(d => d.FirstName))
                .ToListAsync(cancellationToken);

        public async Task<DoctorDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            await Project(_context.Doctors.AsNoTracking().Where(d => d.Id == id)).FirstOrDefaultAsync(cancellationToken);

        public async Task<ServiceResult<DoctorDto>> CreateAsync(DoctorCreationDto dto, CancellationToken cancellationToken = default)
        {
            var error = await ValidateReferencesAsync(dto, cancellationToken);
            if (error != null) return error;

            var doctor = new Doctor { DoctorServices = new List<Domain.Entities.DoctorService>() };
            Apply(doctor, dto);
            _context.Doctors.Add(doctor);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(AuditActions.DoctorCreated, nameof(Doctor), doctor.Id,
                new { doctor.DepartmentId, ServiceIds = dto.ServiceIds.Distinct().ToArray() }, cancellationToken: cancellationToken);
            return ServiceResult<DoctorDto>.Ok((await GetByIdAsync(doctor.Id, cancellationToken))!);
        }

        public async Task<ServiceResult<DoctorDto>> UpdateAsync(int id, DoctorCreationDto dto, CancellationToken cancellationToken = default)
        {
            var doctor = await _context.Doctors.Include(d => d.DoctorServices).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
            if (doctor == null) return ServiceResult<DoctorDto>.NotFound($"Doctor with ID {id} not found");
            var error = await ValidateReferencesAsync(dto, cancellationToken);
            if (error != null) return error;

            _context.DoctorServices.RemoveRange(doctor.DoctorServices);
            doctor.DoctorServices.Clear();
            Apply(doctor, dto);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(AuditActions.DoctorUpdated, nameof(Doctor), doctor.Id,
                new { doctor.DepartmentId, ServiceIds = dto.ServiceIds.Distinct().ToArray() }, cancellationToken: cancellationToken);
            return ServiceResult<DoctorDto>.Ok((await GetByIdAsync(id, cancellationToken))!);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var doctor = await _context.Doctors.FindAsync(new object[] { id }, cancellationToken);
            if (doctor == null) return false;

            _context.Doctors.Remove(doctor);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(AuditActions.DoctorDeleted, nameof(Doctor), id, cancellationToken: cancellationToken);
            return true;
        }

        /// <summary>Unknown department or service ids are refused instead of being silently dropped.</summary>
        private async Task<ServiceResult<DoctorDto>?> ValidateReferencesAsync(DoctorCreationDto dto, CancellationToken cancellationToken)
        {
            if (dto.DepartmentId.HasValue && !await _context.Departments.AnyAsync(d => d.Id == dto.DepartmentId.Value, cancellationToken))
                return ServiceResult<DoctorDto>.Invalid($"Department with ID {dto.DepartmentId} not found");

            var serviceIds = dto.ServiceIds.Distinct().ToArray();
            var known = await _context.Services.CountAsync(s => serviceIds.Contains(s.Id), cancellationToken);
            return known == serviceIds.Length
                ? null
                : ServiceResult<DoctorDto>.Invalid("One or more service IDs do not exist");
        }

        private static void Apply(Doctor doctor, DoctorCreationDto dto)
        {
            doctor.FirstName = dto.FirstName;
            doctor.LastName = dto.LastName;
            doctor.Birthday = dto.Birthday;
            doctor.Address = dto.Address;
            doctor.DepartmentId = dto.DepartmentId;
            foreach (var serviceId in dto.ServiceIds.Distinct())
                doctor.DoctorServices.Add(new Domain.Entities.DoctorService { Doctor = doctor, ServiceId = serviceId });
        }

        private static IQueryable<DoctorDto> Project(IQueryable<Doctor> query) =>
            query.Select(d => new DoctorDto
            {
                Id = d.Id,
                FirstName = d.FirstName,
                LastName = d.LastName,
                Birthday = d.Birthday,
                Address = d.Address,
                DepartmentId = d.DepartmentId,
                ServiceIds = d.DoctorServices.Select(ds => ds.ServiceId).ToArray()
            });
    }
}
