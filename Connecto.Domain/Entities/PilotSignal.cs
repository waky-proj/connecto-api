namespace Connecto.Domain.Entities;

// Durable, actor-bound measurement of a search and an explicit suitability decision.
public sealed class PilotSignal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public Guid NeedId { get; set; }
    public Guid? OfferingId { get; set; }
    public string Kind { get; set; } = "";
    public int? CandidateCount { get; set; }
    public string ClientKey { get; set; } = "";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
