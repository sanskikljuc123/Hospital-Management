using Data;
using Domain.Dtos;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Services.Common;

namespace Services.DepartmentService
{
    public interface IDepartmentService
    {
        Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<DepartmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<ServiceResult<DepartmentDto>> CreateAsync(DepartmentDto dto, CancellationToken cancellationToken = default);
        Task<ServiceResult<DepartmentDto>> UpdateAsync(int id, DepartmentDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }

    public class DepartmentService : IDepartmentService
    {
        private readonly ApplicationDbContext _context;

        public DepartmentService(ApplicationDbContext context) => _context = context;

        public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
            await _context.Departments.AsNoTracking().OrderBy(d => d.Name)
                .Select(d => new DepartmentDto { Id = d.Id, Name = d.Name, Description = d.Description })
                .ToListAsync(cancellationToken);

        public async Task<DepartmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            await _context.Departments.AsNoTracking().Where(d => d.Id == id)
                .Select(d => new DepartmentDto { Id = d.Id, Name = d.Name, Description = d.Description })
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<ServiceResult<DepartmentDto>> CreateAsync(DepartmentDto dto, CancellationToken cancellationToken = default)
        {
            if (await _context.Departments.AnyAsync(d => d.Name == dto.Name, cancellationToken))
                return ServiceResult<DepartmentDto>.Conflict($"A department named '{dto.Name}' already exists");

            var department = new Department { Name = dto.Name, Description = dto.Description };
            _context.Departments.Add(department);
            await _context.SaveChangesAsync(cancellationToken);
            return ServiceResult<DepartmentDto>.Ok(new DepartmentDto { Id = department.Id, Name = department.Name, Description = department.Description });
        }

        public async Task<ServiceResult<DepartmentDto>> UpdateAsync(int id, DepartmentDto dto, CancellationToken cancellationToken = default)
        {
            var department = await _context.Departments.FindAsync(new object[] { id }, cancellationToken);
            if (department == null) return ServiceResult<DepartmentDto>.NotFound($"Department with ID {id} not found");
            if (await _context.Departments.AnyAsync(d => d.Name == dto.Name && d.Id != id, cancellationToken))
                return ServiceResult<DepartmentDto>.Conflict($"A department named '{dto.Name}' already exists");

            department.Name = dto.Name;
            department.Description = dto.Description;
            await _context.SaveChangesAsync(cancellationToken);
            return ServiceResult<DepartmentDto>.Ok(new DepartmentDto { Id = department.Id, Name = department.Name, Description = department.Description });
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var department = await _context.Departments.FindAsync(new object[] { id }, cancellationToken);
            if (department == null) return false;

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
