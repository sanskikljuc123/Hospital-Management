using Domain.Dtos;
using Services.Common;

namespace Services.MedicalRecordService
{
    public interface IMedicalRecordService
    {
        Task<IReadOnlyList<MedicalRecordDto>> GetAllAsync(int? patientId = null, CancellationToken cancellationToken = default);
        Task<MedicalRecordDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<ServiceResult<MedicalRecordDto>> CreateAsync(MedicalRecordCreateDto dto, CancellationToken cancellationToken = default);
        Task<ServiceResult<MedicalRecordDto>> UpdateAsync(int id, MedicalRecordUpdateDto dto, CancellationToken cancellationToken = default);
    }
}
