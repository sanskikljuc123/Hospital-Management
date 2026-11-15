using Domain.Dtos;
using Services.Common;

namespace Services.DoctorService
{
    public interface IDoctorService
    {
        Task<IReadOnlyList<DoctorDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<DoctorDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<ServiceResult<DoctorDto>> CreateAsync(DoctorCreationDto dto, CancellationToken cancellationToken = default);
        Task<ServiceResult<DoctorDto>> UpdateAsync(int id, DoctorCreationDto dto, CancellationToken cancellationToken = default);
        /// <summary>False when the doctor does not exist; a doctor with history is refused by the database (409).</summary>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
