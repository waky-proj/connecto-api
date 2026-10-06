namespace Connecto.Domain.Entities;

public sealed class ConnectionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NeedId { get; set; }
    public Guid OfferingId { get; set; }
    public Guid SenderId { get; set; }
    public Guid RecipientId { get; set; }
    public int NeedRevision { get; set; }
    public int OfferingRevision { get; set; }
    public string Terms { get; set; } = "";
    public string Currency { get; set; } = "NZD";
    public PricingBasis PricingBasis { get; set; }
    public decimal? ProposedAmount { get; set; }
    public string Status { get; set; } = "pending";
    // Null for terminal requests; a filtered unique index prevents concurrent pending duplicates.
    public string? PendingPairKey { get; set; }
    public int Revision { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
}
