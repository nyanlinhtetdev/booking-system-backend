namespace ResourceBookingSystem.WebApp.AuditLog.DTOs;

public sealed class AuditLogResponseDto
{
    public Guid AuditLogId { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? ActorName { get; init; }
    public string? ActorEmail { get; init; }
    public string Action { get; init; } = null!;
    public string EntityType { get; init; } = null!;
    public Guid? EntityId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }
    public string? RequestId { get; init; }
}