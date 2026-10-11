using System.Data;
using System.Text.Json;
using Connecto.Api.Authentication;
using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Api.Controllers;

[ApiController]
public sealed class ReviewsController(ConnectoDbContext db, ICurrentUser currentUser,
    IConfiguration configuration) : ControllerBase
{
    public sealed record SubmitRequest(int? Reliability, int? Communication, int? Agreement,
        int? Clarity, int? Commitments, string? Comment, int PublicationDelayDays, Guid[]? AssetIds = null);
    public sealed record ExcludeRequest(string? Reason);

    private int MaximumWindowDays => int.TryParse(configuration["Reviews:MaximumWindowDays"], out var days) &&
        days is >= 1 and <= 365 ? days : 0;
    private int MinimumDelayDays => int.TryParse(configuration["Reviews:MinimumDelayDays"], out var days) &&
        days is >= 1 and <= 365 ? days : 0;

    [HttpGet("api/agreements/{agreementId:guid}/review-eligibility")]
    public async Task<IActionResult> Eligibility(Guid agreementId, CancellationToken ct)
    {
        var actor = (await currentUser.GetAsync(ct)).Id;
        var agreement = await ParticipantAgreement(agreementId, actor, ct);
        if (agreement is null) return NotFound();
        var own = await db.EngagementReviews.AsNoTracking().SingleOrDefaultAsync(x =>
            x.EngagementId == agreementId && x.ReviewerId == actor, ct);
        var now = DateTime.UtcNow;
        var deadline = Deadline(agreement);
        var eligible = MaximumWindowDays > 0 && agreement.OutcomeState == "both_confirmed" &&
            deadline is not null && now < deadline && own is null;
        return Ok(new {
            agreement.Id, eligible, role = actor == agreement.RequesterId ? "requester" : "provider",
            minimumDelayDays = MinimumDelayDays, maximumDelayDays = MaximumWindowDays,
            windowClosesAt = agreement.OutcomeState == "both_confirmed" ? deadline : null,
            ownStatus = own is null ? "not_submitted" : own.ExcludedAt is not null ? "excluded" :
                own.WithdrawnAt is not null ? "withdrawn" : IsPublished(own, agreement, now) ? "published" : "hidden",
            ownReview = own is null ? null : OwnView(own)
        });
    }

    [HttpPost("api/agreements/{agreementId:guid}/reviews")]
    public async Task<IActionResult> Submit(Guid agreementId, SubmitRequest request, CancellationToken ct)
    {
        var actor = (await currentUser.GetAsync(ct)).Id;
        var visible = await ParticipantAgreement(agreementId, actor, ct);
        if (visible is null) return NotFound();
        var requester = actor == visible.RequesterId;
        var ratings = new[] { request.Reliability, request.Communication, request.Agreement,
            request.Clarity, request.Commitments };
        if (ratings.Any(x => x is < 1 or > 5) || ratings.All(x => x is null) ||
            (requester && (request.Clarity is not null || request.Commitments is not null)) ||
            (!requester && (request.Reliability is not null || request.Agreement is not null)) ||
            request.Comment?.Length > 2000 || MinimumDelayDays == 0 || MaximumWindowDays < MinimumDelayDays ||
            request.PublicationDelayDays < MinimumDelayDays || request.PublicationDelayDays > MaximumWindowDays)
            return BadRequest(new ProblemDetails { Title = "Invalid role-specific review" });
        try
        {
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            if (transaction is not null)
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE [Engagements] SET [OutcomeRevision] = [OutcomeRevision] WHERE [Id] = {agreementId}", ct);
            var agreement = await db.Engagements.SingleAsync(x => x.Id == agreementId, ct);
            var deadline = Deadline(agreement);
            if (MaximumWindowDays == 0 || agreement.OutcomeState != "both_confirmed" ||
                deadline is null || DateTime.UtcNow >= deadline || agreement.RequesterId == agreement.ProviderId)
                return Conflict(new ProblemDetails { Title = "Review submission is not eligible" });
            if (await db.EngagementReviews.AnyAsync(x => x.EngagementId == agreementId && x.ReviewerId == actor, ct))
                return Conflict(new ProblemDetails { Title = "A review was already submitted" });
            var assetIds = request.AssetIds ?? [];
            if (assetIds.Length > 3 || assetIds.Distinct().Count() != assetIds.Length)
                return BadRequest(new ProblemDetails { Title = "Invalid review attachments" });
            var assets = await db.MediaAssets.Where(x => assetIds.Contains(x.Id)).ToListAsync(ct);
            if (assets.Count != assetIds.Length || assets.Any(x => x.EngagementId != agreementId ||
                x.UploaderId != actor || x.Purpose != "review" || x.Stage != "ready" || x.ReviewId != null))
                return BadRequest(new ProblemDetails { Title = "Review attachment is not ready or owned by this reviewer" });
            var review = new EngagementReview {
                EngagementId = agreementId, ReviewerId = actor,
                RevieweeId = requester ? agreement.ProviderId : agreement.RequesterId,
                ReviewerRole = requester ? "requester" : "provider",
                Reliability = request.Reliability, Communication = request.Communication,
                Agreement = request.Agreement, Clarity = request.Clarity,
                Commitments = request.Commitments, Comment = request.Comment?.Trim(),
                PublicationDelayDays = request.PublicationDelayDays,
                PublishAfterAt = agreement.OutcomeUpdatedAt!.Value.AddDays(request.PublicationDelayDays)
            };
            var reciprocal = await db.EngagementReviews.SingleOrDefaultAsync(x =>
                x.EngagementId == agreementId && x.ReviewerId != actor &&
                x.WithdrawnAt == null && x.ExcludedAt == null, ct);
            if (reciprocal is not null)
            {
                var publishedAt = DateTime.UtcNow;
                review.PublishedAt = publishedAt;
                reciprocal.PublishedAt = publishedAt;
            }
            db.EngagementReviews.Add(review);
            foreach (var asset in assets) asset.ReviewId = review.Id;
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Created($"/api/agreements/{agreementId}/review-eligibility", OwnView(review));
        }
        catch (Exception error) when (IsConcurrency(error))
        {
            return Conflict(new ProblemDetails { Title = "Review changed during submission" });
        }
    }

    [HttpGet("api/agreements/{agreementId:guid}/reviews")]
    public async Task<IActionResult> AgreementReviews(Guid agreementId, CancellationToken ct)
    {
        var actor = (await currentUser.GetAsync(ct)).Id;
        var agreement = await ParticipantAgreement(agreementId, actor, ct);
        if (agreement is null) return NotFound();
        var reviews = await db.EngagementReviews.AsNoTracking()
            .Where(x => x.EngagementId == agreementId).ToListAsync(ct);
        var own = reviews.SingleOrDefault(x => x.ReviewerId == actor);
        var other = reviews.SingleOrDefault(x => x.ReviewerId != actor);
        var now = DateTime.UtcNow;
        return Ok(new {
            own = own is null ? null : OwnView(own),
            reciprocal = other is not null && IsPublished(other, agreement, now)
                ? PublicView(other) : null
        });
    }

    [HttpPost("api/agreements/{agreementId:guid}/reviews/mine/withdraw")]
    public async Task<IActionResult> Withdraw(Guid agreementId, CancellationToken ct)
    {
        var actor = (await currentUser.GetAsync(ct)).Id;
        var visible = await ParticipantAgreement(agreementId, actor, ct);
        if (visible is null) return NotFound();
        try
        {
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            if (transaction is not null)
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE [Engagements] SET [OutcomeRevision] = [OutcomeRevision] WHERE [Id] = {agreementId}", ct);
            var review = await db.EngagementReviews.SingleOrDefaultAsync(x =>
                x.EngagementId == agreementId && x.ReviewerId == actor, ct);
            if (review is null) return NotFound();
            if (review.WithdrawnAt is null)
            {
                review.WithdrawnAt = DateTime.UtcNow;
                var assets = await db.MediaAssets.Where(x => x.ReviewId == review.Id && x.Stage != "withdrawn").ToListAsync(ct);
                foreach (var asset in assets) { asset.Stage = "withdrawn"; asset.WithdrawnAt = review.WithdrawnAt; }
                await db.SaveChangesAsync(ct);
            }
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Ok(OwnView(review));
        }
        catch (Exception error) when (IsConcurrency(error))
        {
            return Conflict(new ProblemDetails { Title = "Review changed during withdrawal" });
        }
    }

    [HttpPost("api/reviews/{id:guid}/exclude")]
    public async Task<IActionResult> Exclude(Guid id, ExcludeRequest request, CancellationToken ct)
    {
        if (!IsOperator()) return Forbid();
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            return BadRequest(new ProblemDetails { Title = "An exclusion reason is required" });
        var review = await db.EngagementReviews.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (review is null) return NotFound();
        if (review.ExcludedAt is not null)
            return Conflict(new ProblemDetails { Title = "Review already excluded" });
        review.ExcludedAt = DateTime.UtcNow;
        review.ExclusionReason = reason;
        review.ExcludedBySubjectId = User.FindFirst("sub")?.Value;
        var excludedAssets = await db.MediaAssets.Where(x => x.ReviewId == review.Id && x.Stage != "withdrawn").ToListAsync(ct);
        foreach (var asset in excludedAssets) { asset.Stage = "withdrawn"; asset.WithdrawnAt = review.ExcludedAt; }
        await db.SaveChangesAsync(ct);
        return Ok(new { review.Id, review.ExcludedAt });
    }

    [HttpGet("api/members/{memberId:guid}/reputation")]
    public async Task<IActionResult> Reputation(Guid memberId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == memberId, ct)) return NotFound();
        var now = DateTime.UtcNow;
        var rows = await (from review in db.EngagementReviews.AsNoTracking()
            join engagement in db.Engagements.AsNoTracking() on review.EngagementId equals engagement.Id
            where review.RevieweeId == memberId && review.WithdrawnAt == null &&
                review.ExcludedAt == null && engagement.OutcomeState == "both_confirmed"
            select new { review, engagement }).ToListAsync(ct);
        var published = rows.Where(x => IsPublished(x.review, x.engagement, now))
            .Select(x => x.review).OrderByDescending(x => x.SubmittedAt).ThenBy(x => x.Id).ToList();
        var publicReviewIds = published.Select(x => x.Id).ToArray();
        var publicPhotos = await db.MediaAssets.AsNoTracking().Where(x => x.ReviewId != null &&
            publicReviewIds.Contains(x.ReviewId.Value) && x.Stage == "ready")
            .Select(x => new { x.ReviewId, x.Id }).ToListAsync(ct);
        object Summary(string role)
        {
            var selected = published.Where(x => x.ReviewerRole == role).ToList();
            object Average(Func<EngagementReview, int?> value)
            {
                var values = selected.Select(value).Where(x => x.HasValue).Select(x => x!.Value).ToList();
                return new { count = values.Count, average = values.Count == 0 ? (double?)null :
                    Math.Round(values.Average(), 2, MidpointRounding.AwayFromZero) };
            }
            return new {
                reviewCount = selected.Count, insufficientHistory = selected.Count == 0,
                dimensions = role == "requester" ? (object)new {
                    reliability = Average(x => x.Reliability), communication = Average(x => x.Communication),
                    agreement = Average(x => x.Agreement)
                } : new {
                    communication = Average(x => x.Communication), clarity = Average(x => x.Clarity),
                    commitments = Average(x => x.Commitments)
                }
            };
        }
        return Ok(new {
            memberId, asProvider = Summary("requester"), asRequester = Summary("provider"),
            history = published.GroupBy(x => x.ReviewerRole)
                .SelectMany(group => group.Take(20)).Select(x => PublicView(x,
                    publicPhotos.Where(photo => photo.ReviewId == x.Id).Select(photo => photo.Id).ToArray()))
        });
    }

    private DateTime? Deadline(Engagement agreement) => agreement.OutcomeUpdatedAt?.AddDays(MaximumWindowDays);
    private bool IsPublished(EngagementReview review, Engagement agreement, DateTime now) =>
        review.WithdrawnAt is null && review.ExcludedAt is null &&
        agreement.OutcomeState == "both_confirmed" &&
        (review.PublishedAt is not null || now >= review.PublishAfterAt);

    private static object Ratings(EngagementReview x) => x.ReviewerRole == "requester" ? (object)new {
        x.Reliability, x.Communication, x.Agreement
    } : new { x.Communication, x.Clarity, x.Commitments };
    private static object OwnView(EngagementReview x) => new {
        x.Id, x.EngagementId, x.ReviewerRole, ratings = Ratings(x), x.Comment,
        x.SubmittedAt, x.PublicationDelayDays, x.PublishAfterAt, x.PublishedAt, x.WithdrawnAt, x.ExcludedAt
    };
    private static object PublicView(EngagementReview x, Guid[]? photoIds = null) => new {
        x.Id, x.ReviewerRole, x.SubmittedAt,
        ratings = Ratings(x), x.Comment, photoIds = photoIds ?? []
    };

    private Task<Engagement?> ParticipantAgreement(Guid id, Guid actor, CancellationToken ct) =>
        db.Engagements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id &&
            (x.RequesterId == actor || x.ProviderId == actor), ct);

    private bool IsOperator()
    {
        var group = configuration["Safety:OperatorGroup"];
        if (string.IsNullOrWhiteSpace(group) || User.Identity?.IsAuthenticated != true) return false;
        return User.FindAll("cognito:groups").Any(claim => claim.Value == group ||
            claim.Value.StartsWith('[') && GroupArrayContains(claim.Value, group));
    }
    private static bool GroupArrayContains(string value, string group)
    {
        try
        {
            using var json = JsonDocument.Parse(value);
            return json.RootElement.ValueKind == JsonValueKind.Array &&
                json.RootElement.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && x.GetString() == group);
        }
        catch (JsonException) { return false; }
    }
    private static bool IsConcurrency(Exception error) => error is DbUpdateConcurrencyException ||
        error is SqlException sql && sql.Number is 1205 or 2601 or 2627 or 3960 ||
        error.InnerException is not null && IsConcurrency(error.InnerException);
}
