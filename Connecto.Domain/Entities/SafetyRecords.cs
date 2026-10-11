namespace Connecto.Domain.Entities;

public sealed class MemberBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlockerId { get; set; }
    public Guid BlockedId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class SafetyReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? OfferingId { get; set; }
    public Guid? MediaAssetId { get; set; }
    public string Reason { get; set; } = "";
    public string Details { get; set; } = "";
    public string Status { get; set; } = "open";
    public string? OperatorSubjectId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}

public sealed class OutboxEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "";
    public Guid ActorId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid AggregateId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public int Attempts { get; set; }
}

public sealed class ConsumerReceipt
{
    public Guid EventId { get; set; }
    public string Consumer { get; set; } = "";
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public sealed class JourneyEvent
{
    public Guid EventId { get; set; }
    public string Type { get; set; } = "";
    public Guid ActorId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid AggregateId { get; set; }
    public DateTime OccurredAt { get; set; }
}
