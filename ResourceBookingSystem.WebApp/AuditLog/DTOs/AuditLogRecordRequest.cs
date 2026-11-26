namespace ResourceBookingSystem.WebApp.AuditLog.DTOs;

public sealed record AuditLogRecordRequest(
    Guid ActorUserId,
    string Action,
    string EntityType,
    Guid? EntityId,
    object? OldValues,
    object? NewValues,
    string? IpAddress,
    string? UserAgent,
    string? RequestId);