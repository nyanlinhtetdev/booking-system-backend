using System;
using System.Collections.Generic;

namespace ResourceBookingSystem.Database.AppDbContextModels;

public partial class AuditLog
{
    public Guid AuditLogId { get; set; }

    public Guid? ActorUserId { get; set; }

    public string Action { get; set; } = null!;

    public string EntityType { get; set; } = null!;

    public Guid? EntityId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? RequestId { get; set; }

    public virtual User? ActorUser { get; set; }
}
