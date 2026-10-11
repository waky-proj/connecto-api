namespace Connecto.Domain.Entities;

// Append-only record of participant decisions. Reports are never inferred from timeouts.
public sealed class EngagementOutcomeEvent
{
    public long Id { get; set; }
    public Guid EngagementId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string? ReportedOutcome { get; set; }
    public string? Reason { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public int Revision { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
