using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResourceBookingSystem.Database.AppDbContextModels;
using ResourceBookingSystem.WebApp.AuditLog.DTOs;
using ResourceBookingSystem.WebApp.AuditLog.ServiceInterfaces;
using ResourceBookingSystem.WebApp.Shared.DTOs;
using AuditLogEntity = ResourceBookingSystem.Database.AppDbContextModels.AuditLog;

namespace ResourceBookingSystem.WebApp.AuditLog.Services;

public sealed class AuditLogService(AppDbContext dbContext) : IAuditLogService
{
    public async Task RecordAsync(AuditLogRecordRequest request)
    {
        var auditLog = new AuditLogEntity
        {
            ActorUserId = request.ActorUserId,
            Action = request.Action,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            OccurredAtUtc = DateTime.UtcNow,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            RequestId = request.RequestId,
            OldValues = SerializeValues(request.OldValues),
            NewValues = SerializeValues(request.NewValues)
        };

        await dbContext.AuditLogs.AddAsync(auditLog);
        await dbContext.SaveChangesAsync();
    }

    public async Task<PaginatedResponse<AuditLogResponseDto>> GetAllAsync(
        int pageNumber,
        int pageSize)
    {
        var query = dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(auditLog => auditLog.OccurredAtUtc);

        var totalRecords = await query.CountAsync();

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(auditLog => new AuditLogResponseDto
            {
                AuditLogId = auditLog.AuditLogId,
                ActorUserId = auditLog.ActorUserId,
                ActorName = auditLog.ActorUser == null
                    ? null
                    : auditLog.ActorUser.FullName,
                ActorEmail = auditLog.ActorUser == null
                    ? null
                    : auditLog.ActorUser.Email,
                Action = auditLog.Action,
                EntityType = auditLog.EntityType,
                EntityId = auditLog.EntityId,
                OccurredAtUtc = auditLog.OccurredAtUtc,
                IpAddress = auditLog.IpAddress,
                UserAgent = auditLog.UserAgent,
                OldValues = auditLog.OldValues,
                NewValues = auditLog.NewValues,
                RequestId = auditLog.RequestId
            })
            .ToListAsync();

        return new PaginatedResponse<AuditLogResponseDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
        };
    }

    private static string? SerializeValues(object? values)
    {
        return values is null ? null : JsonSerializer.Serialize(values);
    }
}