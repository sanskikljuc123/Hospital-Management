using Domain.Dtos;
using Services.Common;

namespace Services.UserService
{
    public interface IUserService
    {
        /// <summary>
        /// Ensures the caller has a local profile that mirrors their token, and records a login
        /// audit entry when the token belongs to a new Keycloak session.
        /// Returns false when the local account has been deactivated.
        /// </summary>
        Task<bool> SyncCurrentUserAsync(CancellationToken cancellationToken = default);

        Task<UserDto?> GetCurrentAsync(CancellationToken cancellationToken = default);
        /// <summary>Id of the patient record linked to the caller, if any.</summary>
        Task<int?> GetCurrentPatientIdAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<ServiceResult<UserDto>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);
        Task<ServiceResult<UserDto>> LinkAsync(int id, UserLinkDto link, CancellationToken cancellationToken = default);
    }
}
