using Connecto.Api.Authentication;
using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Connecto.Api.Controllers;

[ApiController]
[Route("api/connection-requests")]
public sealed class ConnectionRequestsController(ConnectoDbContext db, ICurrentUser currentUser) : ControllerBase
{
    public sealed record CreateRequest(Guid NeedId, Guid OfferingId, int NeedRevision,
        int OfferingRevision, string? Terms, string? PricingBasis, decimal? ProposedAmount);
    public sealed record DecisionRequest(int Revision);
    public sealed record AcceptRequest(int Revision, string? IdempotencyKey);
    public sealed record RequestView(Guid Id, Guid NeedId, Guid OfferingId, Guid SenderId,
        Guid RecipientId, string NeedTitle, string OfferingTitle, int NeedRevision,
        int OfferingRevision, string Terms, string Currency, PricingBasis PricingBasis,
        decimal? ProposedAmount, string Status, int Revision, DateTime CreatedAt, DateTime? DecidedAt);

    [HttpPost]
    public async Task<IActionResult> Create(CreateRequest request, CancellationToken cancellationToken)
    {
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        var terms = request.Terms?.Trim();
        if (request.NeedId == Guid.Empty || request.OfferingId == Guid.Empty ||
            request.NeedRevision < 0 || request.OfferingRevision < 0 ||
            string.IsNullOrWhiteSpace(terms) || terms.Length is < 10 or > 2000 ||
            !Enum.TryParse<PricingBasis>(request.PricingBasis, true, out var basis) ||
            !Enum.IsDefined(basis) || basis == PricingBasis.LegacyUnknown ||
            (basis is PricingBasis.Fixed or PricingBasis.Hourly &&
                (request.ProposedAmount is null or < 0 || request.ProposedAmount > 9999999999999999m ||
                 decimal.Round(request.ProposedAmount.Value, 2) != request.ProposedAmount.Value)) ||
            (basis is PricingBasis.Free or PricingBasis.Negotiable && request.ProposedAmount is not null))
            return BadRequest(new ProblemDetails { Title = "Invalid proposed terms" });

        var need = await db.Needs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.NeedId, cancellationToken);
        var offering = await db.Offerings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.OfferingId, cancellationToken);
        if (need is null || offering is null) return NotFound();
        if (need.UserId != actor) return Forbid();
        if (offering.UserId == actor) return BadRequest(new ProblemDetails { Title = "You cannot request your own offering" });
        if (need.Status != NeedStatus.Published || offering.Status != OfferingStatus.Published ||
            need.PricingBasis == PricingBasis.LegacyUnknown || offering.PricingBasis == PricingBasis.LegacyUnknown ||
            need.Currency != "NZD" || offering.Currency != "NZD" ||
            need.Revision != request.NeedRevision || offering.Revision != request.OfferingRevision)
            return Conflict(new ProblemDetails { Title = "Listing changed or is unavailable. Refresh before requesting." });
        if (await db.MemberBlocks.AnyAsync(x =>
            (x.BlockerId == actor && x.BlockedId == offering.UserId) ||
            (x.BlockerId == offering.UserId && x.BlockedId == actor), cancellationToken))
            return Conflict(new ProblemDetails { Title = "This connection is unavailable" });
        if (await db.Engagements.AnyAsync(x => x.NeedId == need.Id, cancellationToken))
            return Conflict(new ProblemDetails { Title = "This Need already has an agreement" });

        var pairKey = $"{need.Id:N}:{offering.Id:N}";
        if (await db.ConnectionRequests.AnyAsync(x => x.PendingPairKey == pairKey, cancellationToken))
            return Conflict(new ProblemDetails { Title = "A pending request already exists for these listings" });
        var item = new ConnectionRequest
        {
            NeedId = need.Id, OfferingId = offering.Id, SenderId = actor,
            RecipientId = offering.UserId, NeedRevision = need.Revision,
            OfferingRevision = offering.Revision, Terms = terms, PricingBasis = basis,
            ProposedAmount = request.ProposedAmount, PendingPairKey = pairKey
        };
        db.ConnectionRequests.Add(item);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await db.ConnectionRequests.AnyAsync(x => x.PendingPairKey == pairKey, cancellationToken))
                return Conflict(new ProblemDetails { Title = "A pending request already exists for these listings" });
            throw;
        }
        return Created($"/api/connection-requests/{item.Id}", View(item, need.Title, offering.Title));
    }

    [HttpGet]
    public async Task<IActionResult> Inbox([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest();
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        var query = db.ConnectionRequests.AsNoTracking()
            .Where(x => x.SenderId == actor || x.RecipientId == actor);
        var total = await query.CountAsync(cancellationToken);
        var rows = await (from item in query
            join need in db.Needs.AsNoTracking() on item.NeedId equals need.Id
            join offering in db.Offerings.AsNoTracking() on item.OfferingId equals offering.Id
            orderby item.CreatedAt descending, item.Id descending
            select new { item, need.Title, OfferingTitle = offering.Title })
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new { requests = rows.Select(x => View(x.item, x.Title, x.OfferingTitle)), total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        var item = await db.ConnectionRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id &&
            (x.SenderId == actor || x.RecipientId == actor), cancellationToken);
        if (item is null) return NotFound();
        var titles = await Titles(item, cancellationToken);
        return Ok(View(item, titles.need, titles.offering));
    }

    [HttpPost("{id:guid}/decline")]
    public Task<IActionResult> Decline(Guid id, DecisionRequest request, CancellationToken cancellationToken) =>
        Decide(id, request.Revision, "declined", cancellationToken);

    [HttpPost("{id:guid}/withdraw")]
    public Task<IActionResult> Withdraw(Guid id, DecisionRequest request, CancellationToken cancellationToken) =>
        Decide(id, request.Revision, "withdrawn", cancellationToken);

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, AcceptRequest request, CancellationToken cancellationToken)
    {
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        var key = request.IdempotencyKey?.Trim();
        if (request.Revision < 1 || string.IsNullOrWhiteSpace(key) || key.Length > 100)
            return BadRequest(new ProblemDetails { Title = "A request revision and idempotency key are required" });

        try { return await AcceptCore(id, request.Revision, key, actor, cancellationToken); }
        catch (Exception error) when (IsConcurrencyError(error))
        {
            return Conflict(new ProblemDetails { Title = "Request or Need changed during acceptance. Refresh the inbox." });
        }
    }

    private static bool IsConcurrencyError(Exception error) =>
        error is DbUpdateConcurrencyException ||
        error is SqlException sql && sql.Number is 1205 or 2601 or 2627 or 3960 ||
        error.InnerException is not null && IsConcurrencyError(error.InnerException);

    private async Task<IActionResult> AcceptCore(Guid id, int revision, string key, Guid actor, CancellationToken cancellationToken)
    {
        var target = await db.ConnectionRequests.AsNoTracking()
            .Where(x => x.Id == id && (x.SenderId == actor || x.RecipientId == actor))
            .Select(x => new { x.NeedId, x.RecipientId }).SingleOrDefaultAsync(cancellationToken);
        if (target is null) return NotFound();
        if (target.RecipientId != actor) return Forbid();
        // SQL Server serializes the listing/capacity read with this write. The unique NeedId
        // index is the final protection when independent requests race for the same Need.
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        if (transaction is not null)
        {
            // All accept attempts for a Need acquire this lock before reading
            // request state or capacity, avoiding shared-to-exclusive deadlocks.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [Needs] SET [Revision] = [Revision] WHERE [Id] = {target.NeedId}", cancellationToken);
        }
        var item = await db.ConnectionRequests.SingleOrDefaultAsync(x => x.Id == id &&
            (x.SenderId == actor || x.RecipientId == actor), cancellationToken);
        if (item is null) return NotFound();
        if (item.RecipientId != actor) return Forbid();
        var existing = await db.Engagements.AsNoTracking().SingleOrDefaultAsync(x => x.ConnectionRequestId == id, cancellationToken);
        if (existing is not null)
            return existing.IdempotencyKey == key && existing.AcceptedRequestRevision == revision
                ? Ok(AgreementView(existing))
                : Conflict(new ProblemDetails { Title = "Request was already accepted with different retry details" });
        if (item.Status != "pending" || item.Revision != revision)
            return Conflict(new ProblemDetails { Title = "Request changed. Refresh the inbox." });

        var need = await db.Needs.SingleAsync(x => x.Id == item.NeedId, cancellationToken);
        var offering = await db.Offerings.SingleAsync(x => x.Id == item.OfferingId, cancellationToken);
        if (need.Status != NeedStatus.Published || offering.Status != OfferingStatus.Published ||
            need.Revision != item.NeedRevision || offering.Revision != item.OfferingRevision ||
            need.UserId != item.SenderId || offering.UserId != item.RecipientId)
            return Conflict(new ProblemDetails { Title = "Listing changed or is unavailable. Refresh before accepting." });
        if (await db.MemberBlocks.AnyAsync(x =>
            (x.BlockerId == item.SenderId && x.BlockedId == item.RecipientId) ||
            (x.BlockerId == item.RecipientId && x.BlockedId == item.SenderId), cancellationToken))
            return Conflict(new ProblemDetails { Title = "This connection is unavailable" });
        if (await db.Engagements.AnyAsync(x => x.NeedId == item.NeedId, cancellationToken))
            return Conflict(new ProblemDetails { Title = "This Need already has an agreement" });

        var agreement = new Engagement
        {
            ConnectionRequestId = item.Id, NeedId = item.NeedId, OfferingId = item.OfferingId,
            RequesterId = item.SenderId, ProviderId = item.RecipientId,
            NeedTitle = need.Title, OfferingTitle = offering.Title,
            NeedRevision = item.NeedRevision, OfferingRevision = item.OfferingRevision,
            Terms = item.Terms, Currency = item.Currency, PricingBasis = item.PricingBasis,
            AgreedAmount = item.ProposedAmount, IdempotencyKey = key,
            AcceptedRequestRevision = revision
        };
        item.Status = "accepted";
        item.PendingPairKey = null;
        item.Revision++;
        item.DecidedAt = agreement.AcceptedAt;
        db.Engagements.Add(agreement);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Ok(AgreementView(agreement));
    }

    [HttpGet("/api/agreements")]
    public async Task<IActionResult> Agreements(CancellationToken cancellationToken)
    {
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        var rows = await db.Engagements.AsNoTracking()
            .Where(x => x.RequesterId == actor || x.ProviderId == actor)
            .OrderByDescending(x => x.AcceptedAt).ToListAsync(cancellationToken);
        return Ok(rows.Select(AgreementView));
    }

    [HttpGet("/api/agreements/{id:guid}")]
    public async Task<IActionResult> Agreement(Guid id, CancellationToken cancellationToken)
    {
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        var row = await db.Engagements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id &&
            (x.RequesterId == actor || x.ProviderId == actor), cancellationToken);
        return row is null ? NotFound() : Ok(AgreementView(row));
    }

    private static object AgreementView(Engagement x) => new
    {
        x.Id, x.ConnectionRequestId, x.NeedId, x.OfferingId, x.RequesterId, x.ProviderId,
        x.NeedTitle, x.OfferingTitle, x.NeedRevision, x.OfferingRevision,
        x.Terms, x.Currency, x.PricingBasis, x.AgreedAmount, x.AcceptedAt
    };

    private async Task<IActionResult> Decide(Guid id, int revision, string status, CancellationToken cancellationToken)
    {
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        var item = await db.ConnectionRequests.SingleOrDefaultAsync(x => x.Id == id &&
            (x.SenderId == actor || x.RecipientId == actor), cancellationToken);
        if (item is null) return NotFound();
        if (status == "declined" ? item.RecipientId != actor : item.SenderId != actor) return Forbid();
        if (item.Status != "pending" || item.Revision != revision)
            return Conflict(new ProblemDetails { Title = "Request changed. Refresh the inbox." });
        item.Status = status;
        item.PendingPairKey = null;
        item.Revision++;
        item.DecidedAt = DateTime.UtcNow;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ProblemDetails { Title = "Request changed. Refresh the inbox." });
        }
        var titles = await Titles(item, cancellationToken);
        return Ok(View(item, titles.need, titles.offering));
    }

    private async Task<(string need, string offering)> Titles(ConnectionRequest item, CancellationToken cancellationToken) =>
        (await db.Needs.Where(x => x.Id == item.NeedId).Select(x => x.Title).SingleAsync(cancellationToken),
         await db.Offerings.Where(x => x.Id == item.OfferingId).Select(x => x.Title).SingleAsync(cancellationToken));

    private static RequestView View(ConnectionRequest item, string need, string offering) =>
        new(item.Id, item.NeedId, item.OfferingId, item.SenderId, item.RecipientId,
            need, offering, item.NeedRevision, item.OfferingRevision, item.Terms,
            item.Currency, item.PricingBasis, item.ProposedAmount, item.Status,
            item.Revision, item.CreatedAt, item.DecidedAt);
}
