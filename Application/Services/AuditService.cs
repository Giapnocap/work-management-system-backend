using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Application.Services
{
    public sealed class AuditService : IAuditService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly AppDbContext _context;

        public AuditService(AppDbContext context)
        {
            _context = context;
        }

        public async Task RecordAsync(
            string entityType,
            Guid entityId,
            string action,
            Guid? actorUserId,
            object? details = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(entityType))
                throw new ArgumentException("Entity type is required.", nameof(entityType));
            if (entityId == Guid.Empty)
                throw new ArgumentException("Entity id is required.", nameof(entityId));
            if (string.IsNullOrWhiteSpace(action))
                throw new ArgumentException("Audit action is required.", nameof(action));

            await _context.AuditLogs.AddAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = entityType.Trim(),
                EntityId = entityId,
                Action = action.Trim(),
                ActorUserId = actorUserId,
                OccurredAt = DateTime.UtcNow,
                DetailsJson = details == null
                    ? null
                    : JsonSerializer.Serialize(details, SerializerOptions)
            }, cancellationToken);
        }

        public async Task<AuditLogPageDto> GetAsync(
            string? entityType,
            Guid? entityId,
            string? action,
            Guid? actorUserId,
            DateTime? from,
            DateTime? to,
            int page,
            int size,
            CancellationToken cancellationToken = default)
        {
            if (from.HasValue && to.HasValue && from.Value > to.Value)
                throw new BusinessException("Audit start time must not be after end time.");

            var paging = Paging.Normalize(page, size, Paging.DefaultHistoryPageSize);
            var query = _context.AuditLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(entityType))
                query = query.Where(log => log.EntityType == entityType.Trim());
            if (entityId.HasValue)
                query = query.Where(log => log.EntityId == entityId.Value);
            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(log => log.Action == action.Trim());
            if (actorUserId.HasValue)
                query = query.Where(log => log.ActorUserId == actorUserId.Value);
            if (from.HasValue)
                query = query.Where(log => log.OccurredAt >= from.Value);
            if (to.HasValue)
                query = query.Where(log => log.OccurredAt <= to.Value);

            var total = await query.CountAsync(cancellationToken);
            var data = await query
                .OrderByDescending(log => log.OccurredAt)
                .ThenByDescending(log => log.Id)
                .Skip((paging.Page - 1) * paging.Size)
                .Take(paging.Size)
                .Select(log => new AuditLogDto
                {
                    Id = log.Id,
                    EntityType = log.EntityType,
                    EntityId = log.EntityId,
                    Action = log.Action,
                    ActorUserId = log.ActorUserId,
                    OccurredAt = log.OccurredAt,
                    DetailsJson = log.DetailsJson
                })
                .ToListAsync(cancellationToken);

            return new AuditLogPageDto
            {
                Total = total,
                Page = paging.Page,
                Size = paging.Size,
                Data = data
            };
        }
    }
}
