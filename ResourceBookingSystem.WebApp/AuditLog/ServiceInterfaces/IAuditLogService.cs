using ResourceBookingSystem.WebApp.AuditLog.DTOs;
using ResourceBookingSystem.WebApp.Shared.DTOs;

namespace ResourceBookingSystem.WebApp.AuditLog.ServiceInterfaces;

public interface IAuditLogService
{
    Task RecordAsync(AuditLogRecordRequest request);

    Task<PaginatedResponse<AuditLogResponseDto>> GetAllAsync(
        int pageNumber,
        int pageSize);
}