using System.Data;
using Connecto.Api.Authentication;
using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Api.Controllers;

[ApiController]
[Route("api/agreements/{agreementId:guid}/outcome")]
public sealed class EngagementOutcomesController(ConnectoDbContext db, ICurrentUser currentUser) : ControllerBase
{
    public sealed record OutcomeRequest(int Revision, string? IdempotencyKey, string? Outcome, string? Reason);

    [HttpGet]
    public async Task<IActionResult> Get(Guid agreementId, CancellationToken cancellationToken)
    {
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        var agreement = await ParticipantAgreement(agreementId, actor, cancellationToken);
        return agreement is null ? NotFound() : Ok(await View(agreement, cancellationToken));
    }

    [HttpPost("report")]
    public Task<IActionResult> Report(Guid agreementId, OutcomeRequest request, CancellationToken cancellationToken) =>
        Change(agreementId, "report", request, cancellationToken);

    [HttpPost("cancel")]
    public Task<IActionResult> Cancel(Guid agreementId, OutcomeRequest request, CancellationToken cancellationToken) =>
        Change(agreementId, "cancel", request, cancellationToken);

    [HttpPost("dispute")]
    public Task<IActionResult> Dispute(Guid agreementId, OutcomeRequest request, CancellationToken cancellationToken) =>
        Change(agreementId, "dispute", request, cancellationToken);

    private async Task<IActionResult> Change(Guid id, string action, OutcomeRequest request, CancellationToken ct)
    {
        var key = request.IdempotencyKey?.Trim();
        var reason = request.Reason?.Trim();
        var outcome = request.Outcome?.Trim().ToLowerInvariant();
        if (request.Revision < 1 || string.IsNullOrWhiteSpace(key) || key.Length > 100 ||
            (action == "report" && (outcome is not ("completed" or "incomplete") || reason?.Length > 1000)) ||
            (action != "report" && (outcome is not null || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)))
            return BadRequest(new ProblemDetails { Title = "Invalid outcome action" });
        var actor = (await currentUser.GetAsync(ct)).Id;
        var visible = await ParticipantAgreement(id, actor, ct);
        if (visible is null) return NotFound();
        try
        {
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            if (transaction is not null)
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE [Engagements] SET [OutcomeRevision] = [OutcomeRevision] WHERE [Id] = {id}", ct);
            var agreement = await db.Engagements.SingleAsync(x => x.Id == id, ct);
            var previous = await db.EngagementOutcomeEvents.AsNoTracking().SingleOrDefaultAsync(x =>
                x.EngagementId == id && x.ActorId == actor && x.IdempotencyKey == key, ct);
            if (previous is not null)
                return previous.Action == action && previous.ReportedOutcome == outcome && previous.Reason == reason &&
                       previous.Revision == request.Revision
                    ? Ok(await View(agreement, ct))
                    : Conflict(new ProblemDetails { Title = "Retry key was used for another outcome action" });
            if (agreement.OutcomeRevision != request.Revision)
                return Conflict(new ProblemDetails { Title = "Outcome changed. Refresh the agreement." });
            var events = await db.EngagementOutcomeEvents.AsNoTracking()
                .Where(x => x.EngagementId == id).OrderBy(x => x.Id).ToListAsync(ct);
            if (agreement.OutcomeState == "disputed" ||
                action == "report" && (agreement.OutcomeState == "cancelled" ||
                    events.Any(x => x.Action == "report" && x.ActorId == actor)) ||
                action == "cancel" && agreement.OutcomeState is "cancelled" or "both_confirmed")
                return Conflict(new ProblemDetails { Title = "This outcome action is no longer available" });
            var entry = new EngagementOutcomeEvent
            {
                EngagementId = id, ActorId = actor, Action = action,
                ReportedOutcome = action == "report" ? outcome : null,
                Reason = reason, IdempotencyKey = key,
                Revision = agreement.OutcomeRevision
            };
            db.EngagementOutcomeEvents.Add(entry);
            agreement.OutcomeRevision++;
            agreement.OutcomeUpdatedAt = entry.CreatedAt;
            agreement.OutcomeState = action switch
            {
                "cancel" => "cancelled",
                "dispute" => "disputed",
                _ => NextReportState(events, outcome!)
            };
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Ok(await View(agreement, ct));
        }
        catch (Exception error) when (IsConcurrency(error))
        {
            return Conflict(new ProblemDetails { Title = "Outcome changed during this action. Refresh the agreement." });
        }
    }

    private static string NextReportState(List<EngagementOutcomeEvent> events, string outcome)
    {
        var earlier = events.FirstOrDefault(x => x.Action == "report");
        if (earlier is null) return "one_reported";
        return earlier.ReportedOutcome == "completed" && outcome == "completed"
            ? "both_confirmed" : "disputed";
    }

    private static bool IsConcurrency(Exception error) =>
        error is DbUpdateConcurrencyException ||
        error is SqlException sql && sql.Number is 1205 or 2601 or 2627 or 3960 ||
        error.InnerException is not null && IsConcurrency(error.InnerException);

    private Task<Engagement?> ParticipantAgreement(Guid id, Guid actor, CancellationToken ct) =>
        db.Engagements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id &&
            (x.RequesterId == actor || x.ProviderId == actor), ct);

    private async Task<object> View(Engagement agreement, CancellationToken ct)
    {
        var history = await db.EngagementOutcomeEvents.AsNoTracking()
            .Where(x => x.EngagementId == agreement.Id).OrderBy(x => x.Id).ToListAsync(ct);
        return new
        {
            agreement.Id, agreement.OutcomeState, agreement.OutcomeRevision, agreement.OutcomeUpdatedAt,
            agreement.DisputeReviewedAt,
            requesterReport = history.LastOrDefault(x => x.Action == "report" && x.ActorId == agreement.RequesterId)?.ReportedOutcome,
            providerReport = history.LastOrDefault(x => x.Action == "report" && x.ActorId == agreement.ProviderId)?.ReportedOutcome,
            history = history.Select(x => new { x.Id, x.ActorId, x.Action, x.ReportedOutcome, x.Reason, x.Revision, x.CreatedAt })
        };
    }
}
