using Data;
using Domain.Constants;
using Domain.Dtos;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Services.AuditService;
using Services.Common;

namespace Services.UserService
{
    public class UserService : IUserService
    {
        private static readonly TimeSpan SyncCacheDuration = TimeSpan.FromMinutes(5);

        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserContext _currentUser;
        private readonly IAuditService _audit;
        private readonly IMemoryCache _cache;

        public UserService(ApplicationDbContext context, ICurrentUserContext currentUser, IAuditService audit, IMemoryCache cache)
        {
            _context = context;
            _currentUser = currentUser;
            _audit = audit;
            _cache = cache;
        }

        private static string CacheKey(string subject) => $"user-sync:{subject}";

        public async Task<bool> SyncCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var subject = _currentUser.Subject;
            if (string.IsNullOrEmpty(subject)) return true; // nothing to mirror (e.g. anonymous endpoint)

            // Avoid a database round trip on every request: once a session has been synced,
            // trust that for a few minutes. Deactivation evicts the entry on this instance.
            var session = _currentUser.SessionId ?? string.Empty;
            if (_cache.TryGetValue(CacheKey(subject), out string? syncedSession) && syncedSession == session)
                return true;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.KeycloakId == subject, cancellationToken);
            var isNew = user == null;
            if (user == null)
            {
                user = new User { KeycloakId = subject, CreatedAt = DateTime.UtcNow };
                _context.Users.Add(user);
            }
            else if (!user.IsActive)
            {
                return false;
            }

            user.UserName = _currentUser.UserName ?? subject;
            user.Email = _currentUser.Email;
            user.FirstName = _currentUser.FirstName;
            user.LastName = _currentUser.LastName;
            user.Role = Roles.All.FirstOrDefault(_currentUser.IsInRole);

            var isLogin = isNew || user.LastSessionId != _currentUser.SessionId;
            if (isLogin)
            {
                user.LastLoginAt = DateTime.UtcNow;
                user.LastSessionId = _currentUser.SessionId;
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException) when (isNew)
            {
                // A concurrent request of the same user created the profile first; that request
                // also recorded the login.
                _context.Entry(user).State = EntityState.Detached;
                isNew = isLogin = false;
            }

            if (isNew)
                await _audit.LogAsync(AuditActions.UserProvisioned, nameof(User), user.Id,
                    new { user.UserName, user.Role }, cancellationToken: cancellationToken);
            if (isLogin)
                await _audit.LogAsync(AuditActions.UserLogin, nameof(User), user.Id,
                    new { user.Role, SessionId = _currentUser.SessionId }, cancellationToken: cancellationToken);

            _cache.Set(CacheKey(subject), session, SyncCacheDuration);
            return true;
        }

        public async Task<UserDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            var subject = _currentUser.Subject;
            if (subject == null) return null;
            return await Query().FirstOrDefaultAsync(u => u.KeycloakId == subject, cancellationToken);
        }

        public async Task<int?> GetCurrentPatientIdAsync(CancellationToken cancellationToken = default)
        {
            var subject = _currentUser.Subject;
            if (subject == null) return null;
            return await _context.Patients
                .Where(p => p.User != null && p.User.KeycloakId == subject)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
            await Query().OrderBy(u => u.UserName).ToListAsync(cancellationToken);

        public async Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            await Query().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        public async Task<ServiceResult<UserDto>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users.FindAsync(new object[] { id }, cancellationToken);
            if (user == null) return ServiceResult<UserDto>.NotFound($"User with ID {id} not found");
            if (!isActive && user.KeycloakId == _currentUser.Subject)
                return ServiceResult<UserDto>.Invalid("You cannot deactivate your own account");

            if (user.IsActive != isActive)
            {
                user.IsActive = isActive;
                await _context.SaveChangesAsync(cancellationToken);
                _cache.Remove(CacheKey(user.KeycloakId));
                await _audit.LogAsync(isActive ? AuditActions.UserActivated : AuditActions.UserDeactivated,
                    nameof(User), user.Id, new { user.UserName }, cancellationToken: cancellationToken);
            }

            return ServiceResult<UserDto>.Ok((await GetByIdAsync(id, cancellationToken))!);
        }

        public async Task<ServiceResult<UserDto>> LinkAsync(int id, UserLinkDto link, CancellationToken cancellationToken = default)
        {
            if (!await _context.Users.AnyAsync(u => u.Id == id, cancellationToken))
                return ServiceResult<UserDto>.NotFound($"User with ID {id} not found");

            Doctor? doctor = null;
            if (link.DoctorId.HasValue)
            {
                doctor = await _context.Doctors.FindAsync(new object[] { link.DoctorId.Value }, cancellationToken);
                if (doctor == null) return ServiceResult<UserDto>.Invalid($"Doctor with ID {link.DoctorId} not found");
                if (doctor.UserId.HasValue && doctor.UserId != id)
                    return ServiceResult<UserDto>.Conflict($"Doctor with ID {link.DoctorId} is already linked to another user");
            }

            Patient? patient = null;
            if (link.PatientId.HasValue)
            {
                patient = await _context.Patients.FindAsync(new object[] { link.PatientId.Value }, cancellationToken);
                if (patient == null) return ServiceResult<UserDto>.Invalid($"Patient with ID {link.PatientId} not found");
                if (patient.UserId.HasValue && patient.UserId != id)
                    return ServiceResult<UserDto>.Conflict($"Patient with ID {link.PatientId} is already linked to another user");
            }

            // Each user owns at most one doctor and one patient record: drop the old links first.
            // Saved separately so the unique indexes never see two rows for the same user.
            foreach (var d in await _context.Doctors.Where(d => d.UserId == id).ToListAsync(cancellationToken))
                d.UserId = null;
            foreach (var p in await _context.Patients.Where(p => p.UserId == id).ToListAsync(cancellationToken))
                p.UserId = null;

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            if (doctor != null) doctor.UserId = id;
            if (patient != null) patient.UserId = id;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _audit.LogAsync(AuditActions.UserLinked, nameof(User), id,
                new { link.DoctorId, link.PatientId }, cancellationToken: cancellationToken);

            return ServiceResult<UserDto>.Ok((await GetByIdAsync(id, cancellationToken))!);
        }

        private IQueryable<UserDto> Query() =>
            _context.Users.AsNoTracking().Select(u => new UserDto
            {
                Id = u.Id,
                KeycloakId = u.KeycloakId,
                UserName = u.UserName,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
                DoctorId = _context.Doctors.Where(d => d.UserId == u.Id).Select(d => (int?)d.Id).FirstOrDefault(),
                PatientId = _context.Patients.Where(p => p.UserId == u.Id).Select(p => (int?)p.Id).FirstOrDefault()
            });
    }
}
