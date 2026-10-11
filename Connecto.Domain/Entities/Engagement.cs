namespace Connecto.Domain.Entities;

// The agreed terms are copied once at acceptance and never updated from listings.
public sealed class Engagement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConnectionRequestId { get; set; }
    public Guid NeedId { get; set; }
    public Guid OfferingId { get; set; }
    public Guid RequesterId { get; set; }
    public Guid ProviderId { get; set; }
    public string NeedTitle { get; set; } = "";
    public string OfferingTitle { get; set; } = "";
    public int NeedRevision { get; set; }
    public int OfferingRevision { get; set; }
    public string Terms { get; set; } = "";
    public string Currency { get; set; } = "NZD";
    public PricingBasis PricingBasis { get; set; }
    public decimal? AgreedAmount { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public int AcceptedRequestRevision { get; set; }
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
    public string OutcomeState { get; set; } = "awaiting_confirmation";
    public int OutcomeRevision { get; set; } = 1;
    public DateTime? OutcomeUpdatedAt { get; set; }
    public DateTime? DisputeReviewedAt { get; set; }
    public string? DisputeReviewerSubjectId { get; set; }
    public string? DisputeReviewNote { get; set; }
}
