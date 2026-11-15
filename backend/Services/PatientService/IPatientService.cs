using Domain.Dtos;
using Services.Common;

namespace Services.PatientService
{
    public interface IPatientService
    {
        Task<IReadOnlyList<PatientDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<PatientDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<ServiceResult<PatientDto>> CreateAsync(PatientCreationDto dto, CancellationToken cancellationToken = default);
        Task<ServiceResult<PatientDto>> UpdateAsync(int id, PatientCreationDto dto, CancellationToken cancellationToken = default);
        /// <summary>False when the patient does not exist; a patient with history is refused by the database (409).</summary>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
