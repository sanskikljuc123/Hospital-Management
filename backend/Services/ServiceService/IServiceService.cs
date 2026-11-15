using Domain.Dtos;
using Services.Common;

namespace Services.ServiceService
{
    public interface IServiceService
    {
        Task<IReadOnlyList<ServiceDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<ServiceDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<ServiceResult<ServiceDto>> CreateAsync(ServiceDto dto, CancellationToken cancellationToken = default);
        Task<ServiceResult<ServiceDto>> UpdateAsync(int id, ServiceDto dto, CancellationToken cancellationToken = default);
        /// <summary>
        /// NotFound when missing; Conflict while patients or doctors still use the service (deleting it must
        /// never delete or orphan them).
        /// </summary>
        Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
