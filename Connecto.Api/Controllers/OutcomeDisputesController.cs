using System.Text.Json;
using Connecto.Api.Authentication;
using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Api.Controllers;

// Operator acknowledgement records handling without deciding contractual liability or rewriting reports.
[ApiController]
[Route("api/outcome-disputes")]
public sealed class OutcomeDisputesController(ConnectoDbContext db, IConfiguration configuration,
    ICurrentUser currentUser) : ControllerBase
{
    public sealed record ReviewRequest(string? Note);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool reviewed = false, CancellationToken ct = default)
    {
        if (!IsOperator()) return Forbid();
        var rows = await db.Engagements.AsNoTracking()
            .Where(x => x.OutcomeState == "disputed" && (x.DisputeReviewedAt != null) == reviewed)
            .OrderBy(x => x.OutcomeUpdatedAt).Take(100)
            .Select(x => new { x.Id, x.RequesterId, x.ProviderId, x.OutcomeUpdatedAt,
                x.DisputeReviewedAt, x.DisputeReviewerSubjectId, x.DisputeReviewNote })
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, ReviewRequest request, CancellationToken ct)
    {
        if (!IsOperator()) return Forbid();
        var note = request.Note?.Trim();
        if (string.IsNullOrWhiteSpace(note) || note.Length > 1000)
            return BadRequest(new ProblemDetails { Title = "A review note is required" });
        var row = await db.Engagements.SingleOrDefaultAsync(x => x.Id == id && x.OutcomeState == "disputed", ct);
        if (row is null) return NotFound();
        if (row.DisputeReviewedAt is not null)
            return Conflict(new ProblemDetails { Title = "Dispute already reviewed" });
        row.DisputeReviewedAt = DateTime.UtcNow;
        row.DisputeReviewerSubjectId = User.FindFirst("sub")?.Value;
        row.DisputeReviewNote = note;
        db.EngagementOutcomeEvents.Add(new EngagementOutcomeEvent
        {
            EngagementId = id, ActorId = (await currentUser.GetAsync(ct)).Id,
            Action = "review", IdempotencyKey = $"operator-review:{id:N}",
            Revision = row.OutcomeRevision
        });
        row.OutcomeRevision++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ProblemDetails { Title = "Dispute changed. Refresh the queue." });
        }
        return Ok(new { row.Id, row.DisputeReviewedAt, row.DisputeReviewerSubjectId, row.DisputeReviewNote });
    }

    private bool IsOperator()
    {
        var group = configuration["Safety:OperatorGroup"];
        return !string.IsNullOrWhiteSpace(group) && User.Identity?.IsAuthenticated == true &&
            User.FindAll("cognito:groups").Any(claim => HasGroup(claim.Value, group));
    }

    private static bool HasGroup(string value, string group)
    {
        if (value == group) return true;
        if (!value.StartsWith('[')) return false;
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Array &&
                document.RootElement.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == group);
        }
        catch (JsonException) { return false; }
    }
}
