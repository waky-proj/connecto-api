namespace Connecto.Domain.Entities;

public sealed class ConversationMessage
{
    public long Id { get; set; }
    public Guid EngagementId { get; set; }
    public Guid SenderId { get; set; }
    public string ClientKey { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public sealed class ConversationReceipt
{
    public Guid EngagementId { get; set; }
    public Guid MemberId { get; set; }
    public long DeliveredThrough { get; set; }
    public long ReadThrough { get; set; }
}
