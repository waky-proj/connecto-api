using Connecto.Api.Authentication;
using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Connecto.Api.Controllers;

[ApiController]
[Route("api/safety")]
public sealed class SafetyController(ConnectoDbContext db, ICurrentUser currentUser,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("blocks")]
    public async Task<IActionResult> Blocks(CancellationToken cancellationToken)
    {
        var id = (await currentUser.GetAsync(cancellationToken)).Id;
        var ids = await db.MemberBlocks.AsNoTracking().Where(x => x.BlockerId == id)
            .OrderBy(x => x.CreatedAt).Select(x => x.BlockedId).ToListAsync(cancellationToken);
        return Ok(new { memberIds = ids });
    }

    [HttpPut("blocks/{memberId:guid}")]
    public async Task<IActionResult> Block(Guid memberId, CancellationToken cancellationToken)
    {
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        if (memberId == actor) return BadRequest(new ProblemDetails { Title = "You cannot block yourself" });
        if (!await db.Users.AnyAsync(x => x.Id == memberId && x.AuthSubjectId != null, cancellationToken))
            return NotFound();
        if (await db.MemberBlocks.AnyAsync(x => x.BlockerId == actor && x.BlockedId == memberId, cancellationToken))
            return NoContent();
        db.MemberBlocks.Add(new MemberBlock { BlockerId = actor, BlockedId = memberId });
        db.OutboxEvents.Add(Event("member.blocked", actor, memberId, memberId));
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await db.MemberBlocks.AnyAsync(x => x.BlockerId == actor && x.BlockedId == memberId, cancellationToken))
                return NoContent();
            throw;
        }
        return NoContent();
    }

    [HttpDelete("blocks/{memberId:guid}")]
    public async Task<IActionResult> Unblock(Guid memberId, CancellationToken cancellationToken)
    {
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        var block = await db.MemberBlocks.SingleOrDefaultAsync(x => x.BlockerId == actor && x.BlockedId == memberId, cancellationToken);
        if (block is null) return NoContent();
        db.MemberBlocks.Remove(block);
        db.OutboxEvents.Add(Event("member.unblocked", actor, memberId, block.Id));
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    public sealed record ReportRequest(Guid SubjectId, Guid? OfferingId, string? Reason, string? Details);

    [HttpPost("reports")]
    public async Task<IActionResult> Report(ReportRequest request, CancellationToken cancellationToken)
    {
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        var reason = request.Reason?.Trim().ToLowerInvariant();
        var details = request.Details?.Trim();
        if (request.SubjectId == actor || request.SubjectId == Guid.Empty ||
            reason is not ("harassment" or "fraud" or "unsafe" or "other") ||
            string.IsNullOrWhiteSpace(details) || details.Length > 2000)
            return BadRequest(new ProblemDetails { Title = "Invalid safety report" });
        if (!await db.Users.AnyAsync(x => x.Id == request.SubjectId && x.AuthSubjectId != null, cancellationToken))
            return NotFound();
        if (request.OfferingId is not null && !await db.Offerings.AnyAsync(
            x => x.Id == request.OfferingId && x.UserId == request.SubjectId, cancellationToken))
            return BadRequest(new ProblemDetails { Title = "Offering does not belong to reported member" });
        var report = new SafetyReport { ReporterId = actor, SubjectId = request.SubjectId,
            OfferingId = request.OfferingId, Reason = reason, Details = details };
        db.SafetyReports.Add(report);
        db.OutboxEvents.Add(Event("safety.reported", actor, request.SubjectId, report.Id));
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/safety/reports/{report.Id}", new { report.Id, report.Status, report.CreatedAt });
    }

    [HttpGet("reports")]
    public async Task<IActionResult> OwnReports(CancellationToken cancellationToken)
    {
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        var reports = await db.SafetyReports.AsNoTracking().Where(x => x.ReporterId == actor)
            .OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new { x.Id, x.SubjectId, x.OfferingId, x.MediaAssetId, x.Reason, x.Details, x.Status, x.CreatedAt })
            .ToListAsync(cancellationToken);
        return Ok(reports);
    }

    [HttpGet("cases")]
    public async Task<IActionResult> Cases([FromQuery] string status = "open", CancellationToken cancellationToken = default)
    {
        if (!IsOperator()) return Forbid();
        if (status is not ("open" or "reviewed")) return BadRequest();
        var cases = await db.SafetyReports.AsNoTracking().Where(x => x.Status == status)
            .OrderBy(x => x.CreatedAt).Take(100)
            .Select(x => new { x.Id, x.ReporterId, x.SubjectId, x.OfferingId, x.MediaAssetId,
                x.Reason, x.Details, x.Status, x.CreatedAt, x.ReviewedAt, x.OperatorSubjectId })
            .ToListAsync(cancellationToken);
        return Ok(cases);
    }

    [HttpGet("cases/{id:guid}")]
    public async Task<IActionResult> Case(Guid id, CancellationToken cancellationToken)
    {
        if (!IsOperator()) return Forbid();
        var report = await db.SafetyReports.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.ReporterId, x.SubjectId, x.OfferingId, x.MediaAssetId,
                x.Reason, x.Details, x.Status, x.CreatedAt, x.ReviewedAt, x.OperatorSubjectId })
            .SingleOrDefaultAsync(cancellationToken);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpPost("cases/{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, CancellationToken cancellationToken)
    {
        if (!IsOperator()) return Forbid();
        var report = await db.SafetyReports.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (report is null) return NotFound();
        if (report.Status == "reviewed") return Conflict(new ProblemDetails { Title = "Case already reviewed" });
        report.Status = "reviewed";
        report.ReviewedAt = DateTime.UtcNow;
        report.OperatorSubjectId = User.FindFirst("sub")?.Value;
        db.OutboxEvents.Add(Event("safety.case.reviewed", (await currentUser.GetAsync(cancellationToken)).Id,
            report.SubjectId, report.Id));
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { report.Id, report.Status, report.ReviewedAt });
    }

    private bool IsOperator()
    {
        var group = configuration["Safety:OperatorGroup"];
        return !string.IsNullOrWhiteSpace(group) && User.Identity?.IsAuthenticated == true &&
            User.FindAll("cognito:groups").Any(claim => HasGroup(claim.Value, group));
    }

    private static bool HasGroup(string claim, string group)
    {
        if (claim == group) return true;
        if (!claim.StartsWith('[')) return false;
        try
        {
            using var value = JsonDocument.Parse(claim);
            return value.RootElement.ValueKind == JsonValueKind.Array &&
                value.RootElement.EnumerateArray().Any(item =>
                    item.ValueKind == JsonValueKind.String && item.GetString() == group);
        }
        catch (JsonException) { return false; }
    }

    private static OutboxEvent Event(string type, Guid actor, Guid subject, Guid aggregate) =>
        new() { Type = type, ActorId = actor, SubjectId = subject, AggregateId = aggregate };
}
