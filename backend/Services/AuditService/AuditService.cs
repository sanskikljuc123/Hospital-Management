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
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ICurrentUserContext _currentUser;
        private readonly ILogger<AuditService> _logger;

        public AuditService(IServiceScopeFactory scopeFactory, ICurrentUserContext currentUser, ILogger<AuditService> logger)
        {
            _scopeFactory = scopeFactory;
            _currentUser = currentUser;
            _logger = logger;
        }

        public async Task LogAsync(string action, string? entityType = null, object? entityId = null,
            object? details = null, bool success = true, CancellationToken cancellationToken = default)
        {
            var entry = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                Action = action,
                EntityType = entityType,
                EntityId = entityId?.ToString(),
                UserId = _currentUser.Subject,
                UserName = _currentUser.UserName,
                IpAddress = _currentUser.IpAddress,
                Details = details == null ? null : JsonSerializer.Serialize(details),
                Success = success
            };

            try
            {
                // A dedicated context keeps the audit write independent of whatever the caller's
                // context is tracking (including changes that just failed to save).
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.AuditLogs.Add(entry);
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write audit entry {Action} for {EntityType} {EntityId}",
                    action, entityType, entry.EntityId);
            }
        }

        public async Task<IReadOnlyList<AuditLogDto>> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var logs = context.AuditLogs.AsNoTracking();
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
