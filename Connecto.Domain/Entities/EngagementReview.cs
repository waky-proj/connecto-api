namespace Connecto.Domain.Entities;

// One durable submission per participant. Identity, role and reviewee come from the agreement.
public sealed class EngagementReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EngagementId { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid RevieweeId { get; set; }
    public string ReviewerRole { get; set; } = "";
    public int? Reliability { get; set; }
    public int? Communication { get; set; }
    public int? Agreement { get; set; }
    public int? Clarity { get; set; }
    public int? Commitments { get; set; }
    public string? Comment { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public int PublicationDelayDays { get; set; }
    public DateTime PublishAfterAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? WithdrawnAt { get; set; }
    public DateTime? ExcludedAt { get; set; }
    public string? ExclusionReason { get; set; }
    public string? ExcludedBySubjectId { get; set; }
}
