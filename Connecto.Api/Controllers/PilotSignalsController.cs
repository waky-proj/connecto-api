using Connecto.Api.Authentication;
using Connecto.Application.Internal;
using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Api.Controllers;

[ApiController]
[Route("api/pilot/signals")]
public sealed class PilotSignalsController(ConnectoDbContext db, ICurrentUser currentUser,
    IMatchService matchService) : ControllerBase
{
    public sealed record SignalRequest(Guid NeedId, Guid? OfferingId, string? ClientKey);

    [HttpPost("discovery")]
    public Task<IActionResult> Discovery(SignalRequest request, CancellationToken ct) => Save("discovery", request, ct);

    [HttpPost("suitable")]
    public Task<IActionResult> Suitable(SignalRequest request, CancellationToken ct) => Save("suitable", request, ct);

    private async Task<IActionResult> Save(string kind, SignalRequest request, CancellationToken ct)
    {
        var key = request.ClientKey?.Trim();
        if (request.NeedId == Guid.Empty || string.IsNullOrWhiteSpace(key) || key.Length > 100 ||
            kind == "discovery" && request.OfferingId is not null ||
            kind == "suitable" && (request.OfferingId is null || request.OfferingId == Guid.Empty))
            return BadRequest(new ProblemDetails { Title = "Invalid pilot signal" });
        var memberId = (await currentUser.GetAsync(ct)).Id;
        var need = await db.Needs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.NeedId && x.UserId == memberId, ct);
        if (need is null) return NotFound();
        var previous = await db.PilotSignals.AsNoTracking().SingleOrDefaultAsync(x =>
            x.MemberId == memberId && x.Kind == kind && x.ClientKey == key, ct);
        if (previous is not null)
            return previous.NeedId == request.NeedId && previous.OfferingId == request.OfferingId
                ? Ok(new { previous.Id, previous.OccurredAt, previous.CandidateCount })
                : Conflict(new ProblemDetails { Title = "Retry key was used for another pilot signal" });
        if (need.Status != NeedStatus.Published) return NotFound();

        int? candidateCount = null;
        if (kind == "discovery")
        {
            var matches = await matchService.FindMatchesAsync(request.NeedId, ct);
            var ids = matches.Select(x => x.OfferingId).ToArray();
            candidateCount = await db.Offerings.AsNoTracking().CountAsync(x => ids.Contains(x.Id) &&
                x.Status == OfferingStatus.Published && x.PricingBasis != PricingBasis.LegacyUnknown &&
                x.UserId != memberId && !db.MemberBlocks.Any(b =>
                    b.BlockerId == memberId && b.BlockedId == x.UserId ||
                    b.BlockerId == x.UserId && b.BlockedId == memberId), ct);
        }
        else
        {
            if (!await db.PilotSignals.AsNoTracking().AnyAsync(x => x.MemberId == memberId &&
                x.NeedId == request.NeedId && x.Kind == "discovery", ct))
                return Conflict(new ProblemDetails { Title = "Search this Need before marking an offering suitable" });
            var offering = await db.Offerings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.OfferingId &&
                x.Status == OfferingStatus.Published && x.PricingBasis != PricingBasis.LegacyUnknown &&
                x.UserId != memberId, ct);
            if (offering is null) return NotFound();
            if (await db.MemberBlocks.AnyAsync(b =>
                b.BlockerId == memberId && b.BlockedId == offering.UserId ||
                b.BlockerId == offering.UserId && b.BlockedId == memberId, ct)) return Conflict();
            var matches = await matchService.FindMatchesAsync(request.NeedId, ct);
            if (!matches.Any(x => x.OfferingId == request.OfferingId))
                return Conflict(new ProblemDetails { Title = "Offering is not an eligible match for this Need" });
        }

        var signal = new PilotSignal { MemberId = memberId, NeedId = request.NeedId,
            OfferingId = request.OfferingId, Kind = kind, CandidateCount = candidateCount, ClientKey = key };
        db.PilotSignals.Add(signal);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var saved = await db.PilotSignals.AsNoTracking().SingleOrDefaultAsync(x =>
                x.MemberId == memberId && x.Kind == kind && x.ClientKey == key, ct);
            if (saved is not null && saved.NeedId == request.NeedId && saved.OfferingId == request.OfferingId)
                return Ok(new { saved.Id, saved.OccurredAt, saved.CandidateCount });
            if (saved is not null) return Conflict(new ProblemDetails { Title = "Retry key was used for another pilot signal" });
            throw;
        }
        return Created($"/api/pilot/signals/{signal.Id}", new { signal.Id, signal.OccurredAt, signal.CandidateCount });
    }
}
