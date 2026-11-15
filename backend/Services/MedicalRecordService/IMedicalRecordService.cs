using Domain.Dtos;
using Services.Common;

namespace Services.MedicalRecordService
{
    public interface IMedicalRecordService
    {
        Task<IReadOnlyList<MedicalRecordDto>> GetAllAsync(int? patientId = null, CancellationToken cancellationToken = default);
        Task<MedicalRecordDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        /// <param name="doctorId">The author: the doctor linked to the caller, never a value from the request.</param>
        Task<ServiceResult<MedicalRecordDto>> CreateAsync(MedicalRecordCreateDto dto, int doctorId, CancellationToken cancellationToken = default);
        /// <param name="doctorId">The editor: the doctor linked to the caller, never a value from the request.</param>
        Task<ServiceResult<MedicalRecordDto>> UpdateAsync(int id, MedicalRecordUpdateDto dto, int doctorId, CancellationToken cancellationToken = default);
        /// <summary>All versions of the record, oldest first; null when the record does not exist.</summary>
        Task<IReadOnlyList<MedicalRecordRevisionDto>?> GetHistoryAsync(int id, CancellationToken cancellationToken = default);
    }
}
