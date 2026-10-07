using Connecto.Api.Authentication;
using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Api.Controllers;

[ApiController]
[Route("api/agreements/{agreementId:guid}/conversation")]
public sealed class ConversationsController(ConnectoDbContext db, ICurrentUser currentUser) : ControllerBase
{
    public sealed record SendRequest(string? Text, string? IdempotencyKey);
    public sealed record ReceiptRequest(long DeliveredThrough, long ReadThrough);

    [HttpGet("messages")]
    public async Task<IActionResult> History(Guid agreementId, [FromQuery] long? after = null,
        [FromQuery] long? before = null, [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > 100 || after < 0 || before <= 0 || (after.HasValue && before.HasValue))
            return BadRequest(new ProblemDetails { Title = "Invalid message cursor" });
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        var agreement = await ParticipantAgreement(agreementId, actor, cancellationToken);
        if (agreement is null) return NotFound();
        var query = db.ConversationMessages.AsNoTracking().Where(x => x.EngagementId == agreementId);
        if (after.HasValue) query = query.Where(x => x.Id > after.Value);
        if (before.HasValue) query = query.Where(x => x.Id < before.Value);
        var descending = !after.HasValue;
        var batch = await (descending ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id))
            .Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasMore = batch.Count > pageSize;
        var messages = batch.Take(pageSize).ToList();
        if (descending) messages.Reverse();
        var receipts = await db.ConversationReceipts.AsNoTracking()
            .Where(x => x.EngagementId == agreementId).ToListAsync(cancellationToken);
        return Ok(new
        {
            messages = messages.Select(View), hasMore,
            requester = ReceiptView(receipts.FirstOrDefault(x => x.MemberId == agreement.RequesterId)),
            provider = ReceiptView(receipts.FirstOrDefault(x => x.MemberId == agreement.ProviderId))
        });
    }

    [HttpPost("messages")]
    public async Task<IActionResult> Send(Guid agreementId, SendRequest request, CancellationToken cancellationToken)
    {
        var text = request.Text?.Trim();
        var key = request.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 2000 ||
            string.IsNullOrWhiteSpace(key) || key.Length > 100)
            return BadRequest(new ProblemDetails { Title = "A message and retry key are required" });
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        var agreement = await ParticipantAgreement(agreementId, actor, cancellationToken);
        if (agreement is null) return NotFound();
        var existing = await db.ConversationMessages.AsNoTracking().SingleOrDefaultAsync(x =>
            x.EngagementId == agreementId && x.SenderId == actor && x.ClientKey == key, cancellationToken);
        if (existing is not null) return existing.Text == text ? Ok(View(existing)) : Conflict(new ProblemDetails { Title = "Retry key used for another message" });
        var recipient = actor == agreement.RequesterId ? agreement.ProviderId : agreement.RequesterId;
        if (await db.MemberBlocks.AnyAsync(x =>
            (x.BlockerId == actor && x.BlockedId == recipient) ||
            (x.BlockerId == recipient && x.BlockedId == actor), cancellationToken))
            return Conflict(new ProblemDetails { Title = "This conversation is unavailable" });
        var message = new ConversationMessage
        {
            EngagementId = agreementId, SenderId = actor, ClientKey = key, Text = text
        };
        db.ConversationMessages.Add(message);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            existing = await db.ConversationMessages.AsNoTracking().SingleOrDefaultAsync(x =>
                x.EngagementId == agreementId && x.SenderId == actor && x.ClientKey == key, cancellationToken);
            if (existing is not null) return existing.Text == text ? Ok(View(existing)) : Conflict(new ProblemDetails { Title = "Retry key used for another message" });
            throw;
        }
        return Created($"/api/agreements/{agreementId}/conversation/messages?after={message.Id - 1}", View(message));
    }

    [HttpPost("receipt")]
    public async Task<IActionResult> Acknowledge(Guid agreementId, ReceiptRequest request, CancellationToken cancellationToken)
    {
        if (request.DeliveredThrough < 0 || request.ReadThrough < 0 || request.ReadThrough > request.DeliveredThrough)
            return BadRequest(new ProblemDetails { Title = "Invalid delivery or read cursor" });
        var actor = (await currentUser.GetAsync(cancellationToken)).Id;
        if (await ParticipantAgreement(agreementId, actor, cancellationToken) is null) return NotFound();
        var latest = await db.ConversationMessages.Where(x => x.EngagementId == agreementId)
            .MaxAsync(x => (long?)x.Id, cancellationToken) ?? 0;
        if (request.DeliveredThrough > latest) return BadRequest(new ProblemDetails { Title = "Cursor is beyond conversation history" });
        // A receipt may only advance to a message actually in this conversation.
        if (request.DeliveredThrough > 0 && !await db.ConversationMessages.AnyAsync(x => x.EngagementId == agreementId && x.Id == request.DeliveredThrough, cancellationToken))
            return BadRequest(new ProblemDetails { Title = "Unknown delivery cursor" });
        if (request.ReadThrough > 0 && !await db.ConversationMessages.AnyAsync(x => x.EngagementId == agreementId && x.Id == request.ReadThrough, cancellationToken))
            return BadRequest(new ProblemDetails { Title = "Unknown read cursor" });
        if (db.Database.IsRelational())
        {
            var updated = await db.ConversationReceipts.Where(x => x.EngagementId == agreementId && x.MemberId == actor)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.DeliveredThrough, x => x.DeliveredThrough < request.DeliveredThrough ? request.DeliveredThrough : x.DeliveredThrough)
                    .SetProperty(x => x.ReadThrough, x => x.ReadThrough < request.ReadThrough ? request.ReadThrough : x.ReadThrough), cancellationToken);
            if (updated == 0)
            {
                db.ConversationReceipts.Add(new ConversationReceipt { EngagementId = agreementId, MemberId = actor,
                    DeliveredThrough = request.DeliveredThrough, ReadThrough = request.ReadThrough });
                try { await db.SaveChangesAsync(cancellationToken); }
                catch (DbUpdateException)
                {
                    db.ChangeTracker.Clear();
                    await db.ConversationReceipts.Where(x => x.EngagementId == agreementId && x.MemberId == actor)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(x => x.DeliveredThrough, x => x.DeliveredThrough < request.DeliveredThrough ? request.DeliveredThrough : x.DeliveredThrough)
                            .SetProperty(x => x.ReadThrough, x => x.ReadThrough < request.ReadThrough ? request.ReadThrough : x.ReadThrough), cancellationToken);
                }
            }
        }
        else
        {
            var receipt = await db.ConversationReceipts.SingleOrDefaultAsync(x => x.EngagementId == agreementId && x.MemberId == actor, cancellationToken);
            if (receipt is null)
            {
                receipt = new ConversationReceipt { EngagementId = agreementId, MemberId = actor };
                db.ConversationReceipts.Add(receipt);
            }
            receipt.DeliveredThrough = Math.Max(receipt.DeliveredThrough, request.DeliveredThrough);
            receipt.ReadThrough = Math.Max(receipt.ReadThrough, request.ReadThrough);
            await db.SaveChangesAsync(cancellationToken);
        }
        var saved = await db.ConversationReceipts.AsNoTracking().SingleAsync(x => x.EngagementId == agreementId && x.MemberId == actor, cancellationToken);
        return Ok(ReceiptView(saved));
    }

    private Task<Engagement?> ParticipantAgreement(Guid id, Guid actor, CancellationToken cancellationToken) =>
        db.Engagements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id &&
            (x.RequesterId == actor || x.ProviderId == actor), cancellationToken);

    private static object View(ConversationMessage message) => new
    {
        message.Id, message.EngagementId, message.SenderId, message.Text, message.SentAt
    };

    private static object ReceiptView(ConversationReceipt? receipt) => new
    {
        deliveredThrough = receipt?.DeliveredThrough ?? 0,
        readThrough = receipt?.ReadThrough ?? 0
    };
}
