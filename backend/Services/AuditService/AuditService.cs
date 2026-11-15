using System.Text.Json;
using Data;
using Domain.Dtos;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Services.Common;

namespace Services.AuditService
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserContext _currentUser;
        private readonly TimeProvider _time;

        public AuditService(ApplicationDbContext context, ICurrentUserContext currentUser, TimeProvider time)
        {
            _context = context;
            _currentUser = currentUser;
            _time = time;
        }

        public async Task LogAsync(string action, string? entityType = null, object? entityId = null,
            object? details = null, bool success = true, CancellationToken cancellationToken = default)
        {
            var entry = new AuditLog
            {
                Timestamp = _time.GetUtcNow().UtcDateTime,
                Action = action,
                EntityType = entityType,
                EntityId = entityId?.ToString(),
                UserId = _currentUser.Subject,
                UserName = _currentUser.UserName,
                IpAddress = _currentUser.IpAddress,
                Details = details == null ? null : JsonSerializer.Serialize(details),
                Success = success
            };

            // Same DbContext, so inside the request transaction (TransactionalWriteFilter) the entry commits or
            // rolls back together with the change it describes. A failure is not swallowed: no change without
            // its audit entry (decision D-8).
            _context.AuditLogs.Add(entry);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AuditLogDto>> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
        {
            var logs = _context.AuditLogs.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(query.Action)) logs = logs.Where(a => a.Action == query.Action);
            if (!string.IsNullOrWhiteSpace(query.UserId)) logs = logs.Where(a => a.UserId == query.UserId);
            if (!string.IsNullOrWhiteSpace(query.EntityType)) logs = logs.Where(a => a.EntityType == query.EntityType);
            if (!string.IsNullOrWhiteSpace(query.EntityId)) logs = logs.Where(a => a.EntityId == query.EntityId);
            if (query.From.HasValue) logs = logs.Where(a => a.Timestamp >= query.From.Value);
            if (query.To.HasValue) logs = logs.Where(a => a.Timestamp <= query.To.Value);

            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 200);

            return await logs
                .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    Timestamp = a.Timestamp,
                    Action = a.Action,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    UserId = a.UserId,
                    UserName = a.UserName,
                    IpAddress = a.IpAddress,
                    Details = a.Details,
                    Success = a.Success
                })
                .ToListAsync(cancellationToken);
        }
    }
}
