namespace Connecto.Domain.Requests
{
    public sealed record CreateNeedRequest(
        string Title,
        string Description,
        string Category,
        IReadOnlyCollection<string> Skills,
        decimal? BudgetMin,
        decimal? BudgetMax,
        string? Location,
        DateTime? Deadline);
}
