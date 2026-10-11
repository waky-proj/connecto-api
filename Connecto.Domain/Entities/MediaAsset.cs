namespace Connecto.Domain.Entities;

public sealed class MediaAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EngagementId { get; set; }
    public Guid UploaderId { get; set; }
    public Guid? ReviewId { get; set; }
    public string Purpose { get; set; } = ""; // review or case
    public string Kind { get; set; } = ""; // before, after, outcome or evidence
    public string Stage { get; set; } = "pending"; // pending, ready, rejected, withdrawn
    public string QuarantineKey { get; set; } = "";
    public string? DisplayKey { get; set; }
    public string? ContentType { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UploadExpiresAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? WithdrawnAt { get; set; }
    public string? RemovalReason { get; set; }
    public string? RemovedBySubjectId { get; set; }
    public DateTime? DeletedAt { get; set; }
}
