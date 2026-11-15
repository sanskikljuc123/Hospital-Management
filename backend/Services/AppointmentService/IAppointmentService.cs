using Domain.Dtos;
using Services.Common;

namespace Services.AppointmentService
{
    public interface IAppointmentService
    {
        Task<IReadOnlyList<AppointmentDto>> GetAllAsync(int? patientId = null, int? doctorId = null,
            DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
        Task<AppointmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<ServiceResult<AppointmentDto>> CreateAsync(AppointmentCreateDto dto, CancellationToken cancellationToken = default);
        Task<ServiceResult<AppointmentDto>> UpdateAsync(int id, AppointmentUpdateDto dto, CancellationToken cancellationToken = default);
        Task<ServiceResult<AppointmentDto>> CancelAsync(int id, CancellationToken cancellationToken = default);
        /// <summary>
        /// Corrects a final status set by mistake: NoShow ⇄ Completed, or Cancelled → Scheduled while the
        /// appointment has not started and the doctor is still free. The reason is audited.
        /// </summary>
        Task<ServiceResult<AppointmentDto>> CorrectStatusAsync(int id, AppointmentStatusCorrectionDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
