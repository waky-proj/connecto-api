namespace Connecto.Domain.Entities;

public sealed class MediaAccessAudit
{
    public long Id { get; set; }
    public Guid MediaAssetId { get; set; }
    public Guid ActorId { get; set; }
    public string Purpose { get; set; } = "case_evidence_read";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
