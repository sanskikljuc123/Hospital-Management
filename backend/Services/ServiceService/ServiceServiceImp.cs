using Data;
using Domain.Constants;
using Domain.Dtos;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditService;
using Services.Common;

namespace Services.ServiceService
{
    public class ServiceServiceImp : IServiceService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;

        public ServiceServiceImp(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IReadOnlyList<ServiceDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
            await Project(_context.Services.AsNoTracking().OrderBy(s => s.Name)).ToListAsync(cancellationToken);

        public async Task<ServiceDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            await Project(_context.Services.AsNoTracking().Where(s => s.Id == id)).FirstOrDefaultAsync(cancellationToken);

        public async Task<ServiceResult<ServiceDto>> CreateAsync(ServiceDto dto, CancellationToken cancellationToken = default)
        {
            var error = await ValidateDepartmentAsync(dto, cancellationToken);
            if (error != null) return error;

            // Only the documented fields are copied: the request can never create or link patients or doctors.
            var service = new Service { Name = dto.Name, Description = dto.Description, DepartmentId = dto.DepartmentId };
            _context.Services.Add(service);
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(AuditActions.ServiceCreated, nameof(Service), service.Id,
                new { service.Name, service.DepartmentId }, cancellationToken: cancellationToken);
            return ServiceResult<ServiceDto>.Ok((await GetByIdAsync(service.Id, cancellationToken))!);
        }

        public async Task<ServiceResult<ServiceDto>> UpdateAsync(int id, ServiceDto dto, CancellationToken cancellationToken = default)
        {
            var service = await _context.Services.FindAsync(new object[] { id }, cancellationToken);
            if (service == null) return ServiceResult<ServiceDto>.NotFound($"Service with ID {id} not found");
            var error = await ValidateDepartmentAsync(dto, cancellationToken);
            if (error != null) return error;

            service.Name = dto.Name;
            service.Description = dto.Description;
            service.DepartmentId = dto.DepartmentId;
            await _context.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(AuditActions.ServiceUpdated, nameof(Service), service.Id,
                new { service.Name, service.DepartmentId }, cancellationToken: cancellationToken);
            return ServiceResult<ServiceDto>.Ok((await GetByIdAsync(id, cancellationToken))!);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var service = await _context.Services.FindAsync(new object[] { id }, cancellationToken);
            if (service == null) return ServiceResult<bool>.NotFound($"Service with ID {id} not found");
            if (await _context.Patients.AnyAsync(p => p.ServiceId == id, cancellationToken)
                || await _context.DoctorServices.AnyAsync(ds => ds.ServiceId == id, cancellationToken))
                return ServiceResult<bool>.Conflict("The service is still used by patients or doctors");

            _context.Services.Remove(service);
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(AuditActions.ServiceDeleted, nameof(Service), id, cancellationToken: cancellationToken);
            return ServiceResult<bool>.Ok(true);
        }

        private async Task<ServiceResult<ServiceDto>?> ValidateDepartmentAsync(ServiceDto dto, CancellationToken cancellationToken) =>
            dto.DepartmentId.HasValue && !await _context.Departments.AnyAsync(d => d.Id == dto.DepartmentId.Value, cancellationToken)
                ? ServiceResult<ServiceDto>.Invalid($"Department with ID {dto.DepartmentId} not found")
                : null;

        private static IQueryable<ServiceDto> Project(IQueryable<Service> query) =>
            query.Select(s => new ServiceDto { Id = s.Id, Name = s.Name ?? string.Empty, Description = s.Description, DepartmentId = s.DepartmentId });
    }
}
