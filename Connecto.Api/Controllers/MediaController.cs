using Amazon.S3;
using Connecto.Api.Authentication;
using Connecto.Api.Media;
using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using System.Text.Json;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Connecto.Api.Controllers;

[ApiController]
[Route("api/media")]
public sealed class MediaController(ConnectoDbContext db, ICurrentUser currentUser,
    IMediaObjectStore store, IConfiguration config, ILogger<MediaController> logger) : ControllerBase
{
    private const long MaxBytes = 5 * 1024 * 1024;
    public sealed record GrantRequest(Guid AgreementId, string Purpose, string Kind, string ContentType);
    public sealed record ReportRequest(string? Reason, string? Details);
    public sealed record RemoveRequest(string? Reason);

    [HttpPost("upload-grants")]
    public async Task<IActionResult> Grant(GrantRequest request, CancellationToken ct)
    {
        if (!store.IsAvailable) return StatusCode(503, new ProblemDetails { Title = "Media storage unavailable" });
        var actor = (await currentUser.GetAsync(ct)).Id;
        if (request.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            return BadRequest(new ProblemDetails { Title = "Unsupported image type" });
        try {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (transaction is not null)
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [Engagements] SET [OutcomeRevision] = [OutcomeRevision] WHERE [Id] = {request.AgreementId}", ct);
        var agreement = await db.Engagements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.AgreementId &&
            (x.RequesterId == actor || x.ProviderId == actor), ct);
        if (agreement is null) return NotFound();
        var review = request.Purpose == "review" && new[] { "before", "after", "outcome" }.Contains(request.Kind) &&
            agreement.OutcomeState == "both_confirmed" && agreement.OutcomeUpdatedAt is not null &&
            DateTime.UtcNow < agreement.OutcomeUpdatedAt.Value.AddDays(30) &&
            !await db.EngagementReviews.AnyAsync(x => x.EngagementId == agreement.Id && x.ReviewerId == actor, ct);
        var evidence = request.Purpose == "case" && request.Kind == "evidence" && agreement.OutcomeState == "disputed";
        if (!review && !evidence) return Conflict(new ProblemDetails { Title = "Media parent is not eligible" });
        var now = DateTime.UtcNow;
        var count = await db.MediaAssets.CountAsync(x => x.EngagementId == agreement.Id && x.UploaderId == actor &&
            x.Purpose == request.Purpose && (x.Stage == "ready" ||
                x.Stage == "pending" && x.UploadExpiresAt > now), ct);
        if (count >= (review ? 3 : 5)) return Conflict(new ProblemDetails { Title = "Attachment limit reached" });
        var asset = new MediaAsset {
            EngagementId = agreement.Id, UploaderId = actor, Purpose = request.Purpose, Kind = request.Kind,
            ContentType = request.ContentType, UploadExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        asset.QuarantineKey = $"quarantine/{asset.Id:N}";
        var grant = await store.UploadGrantAsync(asset.QuarantineKey, asset.ContentType, asset.UploadExpiresAt);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Created($"/api/media/{asset.Id}", new {
            asset.Id, asset.UploadExpiresAt, maxBytes = MaxBytes, method = "POST",
            contentType = asset.ContentType, uploadUrl = grant.Url, fields = grant.Fields
        });
        }
        catch (Exception error) when (error is DbUpdateConcurrencyException ||
            error is SqlException sql && sql.Number is 1205 or 2601 or 2627 or 3960)
        { return Conflict(new ProblemDetails { Title = "Media grant changed concurrently" }); }
    }

    [HttpPost("{id:guid}/finalise")]
    public async Task<IActionResult> Finalise(Guid id, CancellationToken ct)
    {
        if (!store.IsAvailable) return StatusCode(503, new ProblemDetails { Title = "Media storage unavailable" });
        var actor = (await currentUser.GetAsync(ct)).Id;
        var asset = await db.MediaAssets.SingleOrDefaultAsync(x => x.Id == id && x.UploaderId == actor, ct);
        if (asset is null) return NotFound();
        if (asset.Stage != "pending") return Conflict(new ProblemDetails { Title = "Upload is no longer pending" });
        if (DateTime.UtcNow > asset.UploadExpiresAt) return await Reject(asset, "Upload grant expired", ct);
        var agreement = await db.Engagements.AsNoTracking().SingleAsync(x => x.Id == asset.EngagementId, ct);
        if (asset.Purpose == "review" && (agreement.OutcomeState != "both_confirmed" ||
            await db.EngagementReviews.AnyAsync(x => x.EngagementId == asset.EngagementId && x.ReviewerId == actor, ct)) ||
            asset.Purpose == "case" && agreement.OutcomeState != "disputed")
            return await Reject(asset, "Parent is no longer eligible", ct);
        try
        {
            var (body, length) = await store.ReadAsync(asset.QuarantineKey, ct);
            await using (body)
            {
                if (length is <= 0 or > MaxBytes) return await Reject(asset, "Image size is invalid", ct);
                using var input = new MemoryStream();
                await body.CopyToAsync(input, ct);
                if (input.Length != length || input.Length > MaxBytes) return await Reject(asset, "Image size is invalid", ct);
                using var data = SKData.CreateCopy(input.ToArray());
                using var codec = SKCodec.Create(data);
                var actualType = codec?.EncodedFormat switch {
                    SKEncodedImageFormat.Jpeg => "image/jpeg",
                    SKEncodedImageFormat.Png => "image/png",
                    SKEncodedImageFormat.Webp => "image/webp", _ => null
                };
                if (actualType is null || actualType != asset.ContentType)
                    return await Reject(asset, "Image format does not match upload type", ct);
                if (codec!.FrameCount > 1 || codec.Info.Width < 1 || codec.Info.Height < 1 ||
                    (long)codec.Info.Width * codec.Info.Height > 12_000_000)
                    return await Reject(asset, "Image dimensions or frames are invalid", ct);
                using var decoded = SKBitmap.Decode(data);
                if (decoded is null) return await Reject(asset, "Image could not be decoded", ct);
                var ratio = Math.Min(1d, 1600d / Math.Max(decoded.Width, decoded.Height));
                var rotated = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or
                    SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
                var width = Math.Max(1, (int)((rotated ? decoded.Height : decoded.Width) * ratio));
                var height = Math.Max(1, (int)((rotated ? decoded.Width : decoded.Height) * ratio));
                using var display = new SKBitmap(width, height);
                using (var canvas = new SKCanvas(display)) {
                    canvas.Clear(SKColors.White);
                    canvas.SetMatrix(OrientationMatrix(codec.EncodedOrigin, decoded.Width, decoded.Height, (float)ratio));
                    canvas.DrawBitmap(decoded, new SKRect(0, 0, decoded.Width, decoded.Height), SKSamplingOptions.Default);
                }
                using var picture = SKImage.FromBitmap(display);
                using var jpeg = picture.Encode(SKEncodedImageFormat.Jpeg, 82);
                if (jpeg is null) return await Reject(asset, "Image encoding failed", ct);
                using var output = new MemoryStream(jpeg.ToArray());
                output.Position = 0;
                asset.DisplayKey = $"display/{asset.Id:N}.jpg";
                await store.WriteAsync(asset.DisplayKey, output, "image/jpeg", ct);
                asset.Stage = "ready";
                asset.ContentType = "image/jpeg";
                asset.ProcessedAt = DateTime.UtcNow;
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateConcurrencyException) {
                    try { await store.DeleteAsync(asset.DisplayKey, ct); }
                    catch (Exception error) when (!ct.IsCancellationRequested)
                    { logger.LogWarning(error, "Orphaned media variant requires cleanup for {AssetId}", asset.Id); }
                    return Conflict(new ProblemDetails { Title = "Media state changed during processing" });
                }
            }
            try { await store.DeleteAsync(asset.QuarantineKey, ct); }
            catch (Exception error) when (!ct.IsCancellationRequested)
            { logger.LogWarning(error, "Quarantine cleanup deferred for {AssetId}", asset.Id); }
            return Ok(View(asset));
        }
        catch (AmazonS3Exception error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound)
        { return Conflict(new ProblemDetails { Title = "Upload has not reached quarantine" }); }
    }

    [HttpGet("agreements/{agreementId:guid}")]
    public async Task<IActionResult> List(Guid agreementId, CancellationToken ct)
    {
        var actor = (await currentUser.GetAsync(ct)).Id;
        var agreement = await db.Engagements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == agreementId &&
            (x.RequesterId == actor || x.ProviderId == actor), ct);
        if (agreement is null) return NotFound();
        var rows = await db.MediaAssets.AsNoTracking().Where(x => x.EngagementId == agreementId &&
            x.Stage != "withdrawn").OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var reviews = await db.EngagementReviews.AsNoTracking().Where(x => x.EngagementId == agreementId).ToListAsync(ct);
        return Ok(rows.Where(x => x.UploaderId == actor || CanReadReview(x, agreement, reviews, DateTime.UtcNow))
            .Where(x => x.Purpose != "case" || x.UploaderId == actor || IsOperator())
            .Select(View));
    }

    [HttpGet("agreements/{agreementId:guid}/case-evidence")]
    public async Task<IActionResult> CaseEvidence(Guid agreementId, CancellationToken ct)
    {
        if (!IsOperator()) return Forbid();
        if (!await db.Engagements.AnyAsync(x => x.Id == agreementId, ct)) return NotFound();
        var rows = await db.MediaAssets.AsNoTracking().Where(x => x.EngagementId == agreementId &&
            x.Purpose == "case" && x.Stage == "ready").OrderBy(x => x.CreatedAt).ToListAsync(ct);
        return Ok(rows.Select(View));
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        var actor = (await currentUser.GetAsync(ct)).Id;
        var asset = await db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Stage == "ready", ct);
        if (asset is null || asset.DisplayKey is null) return NotFound();
        var agreement = await db.Engagements.AsNoTracking().SingleAsync(x => x.Id == asset.EngagementId, ct);
        var review = asset.ReviewId is null ? null : await db.EngagementReviews.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == asset.ReviewId, ct);
        var staffCaseRead = asset.Purpose == "case" && asset.UploaderId != actor && IsOperator();
        var allowed = asset.Purpose == "case" ? asset.UploaderId == actor || staffCaseRead :
            asset.UploaderId == actor && review?.WithdrawnAt is null && review?.ExcludedAt is null ||
            review is not null && IsPublished(review, agreement, DateTime.UtcNow);
        if (!allowed) return NotFound();
        if (staffCaseRead) {
            db.MediaAccessAudits.Add(new MediaAccessAudit { MediaAssetId = asset.Id, ActorId = actor });
            await db.SaveChangesAsync(ct);
        }
        try {
            var (body, _) = await store.ReadAsync(asset.DisplayKey, ct);
            return File(body, "image/jpeg");
        }
        catch (AmazonS3Exception error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound)
        { return NotFound(); }
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        var actor = (await currentUser.GetAsync(ct)).Id;
        var asset = await db.MediaAssets.SingleOrDefaultAsync(x => x.Id == id && x.UploaderId == actor, ct);
        if (asset is null) return NotFound();
        if (asset.Stage != "withdrawn") {
            asset.Stage = "withdrawn";
            asset.WithdrawnAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Ok(View(asset));
    }

    [HttpPost("{id:guid}/report")]
    public async Task<IActionResult> Report(Guid id, ReportRequest request, CancellationToken ct)
    {
        var actor = (await currentUser.GetAsync(ct)).Id;
        var asset = await db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id &&
            x.Purpose == "review" && x.Stage == "ready", ct);
        if (asset is null || asset.UploaderId == actor || asset.ReviewId is null) return NotFound();
        var review = await db.EngagementReviews.AsNoTracking().SingleAsync(x => x.Id == asset.ReviewId, ct);
        var agreement = await db.Engagements.AsNoTracking().SingleAsync(x => x.Id == asset.EngagementId, ct);
        if (!IsPublished(review, agreement, DateTime.UtcNow)) return NotFound();
        var reason = request.Reason?.Trim().ToLowerInvariant();
        var details = request.Details?.Trim();
        if (reason is not ("personal_information" or "inappropriate" or "misleading" or "abuse") ||
            string.IsNullOrWhiteSpace(details) || details.Length > 2000)
            return BadRequest(new ProblemDetails { Title = "Invalid image report" });
        var report = new SafetyReport { ReporterId = actor, SubjectId = asset.UploaderId,
            MediaAssetId = asset.Id, Reason = reason, Details = details };
        db.SafetyReports.Add(report);
        db.OutboxEvents.Add(new OutboxEvent { Type = "safety.media.reported", ActorId = actor,
            SubjectId = asset.UploaderId, AggregateId = report.Id });
        await db.SaveChangesAsync(ct);
        return Created($"/api/safety/reports/{report.Id}", new { report.Id, report.Status });
    }

    [HttpPost("{id:guid}/exclude")]
    public async Task<IActionResult> Exclude(Guid id, RemoveRequest request, CancellationToken ct)
    {
        if (!IsOperator()) return Forbid();
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            return BadRequest(new ProblemDetails { Title = "A removal reason is required" });
        var asset = await db.MediaAssets.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (asset is null) return NotFound();
        if (asset.Stage == "withdrawn") return Conflict(new ProblemDetails { Title = "Image already withdrawn" });
        asset.Stage = "withdrawn";
        asset.WithdrawnAt = DateTime.UtcNow;
        asset.RemovalReason = reason;
        asset.RemovedBySubjectId = User.FindFirst("sub")?.Value;
        await db.SaveChangesAsync(ct);
        return Ok(new { asset.Id, asset.WithdrawnAt });
    }

    private async Task<IActionResult> Reject(MediaAsset asset, string reason, CancellationToken ct)
    {
        asset.Stage = "rejected";
        asset.RejectionReason = reason;
        asset.ProcessedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return UnprocessableEntity(new ProblemDetails { Title = reason });
    }

    private static object View(MediaAsset x) => new {
        x.Id, x.EngagementId, x.UploaderId, x.ReviewId, x.Purpose, x.Kind, x.Stage, x.RejectionReason,
        x.CreatedAt, x.ProcessedAt, x.WithdrawnAt
    };
    private static SKMatrix OrientationMatrix(SKEncodedOrigin origin, int width, int height, float ratio)
    {
        var (a, b, tx, c, d, ty) = origin switch {
            SKEncodedOrigin.TopRight => (-1, 0, width, 0, 1, 0),
            SKEncodedOrigin.BottomRight => (-1, 0, width, 0, -1, height),
            SKEncodedOrigin.BottomLeft => (1, 0, 0, 0, -1, height),
            SKEncodedOrigin.LeftTop => (0, 1, 0, 1, 0, 0),
            SKEncodedOrigin.RightTop => (0, -1, height, 1, 0, 0),
            SKEncodedOrigin.RightBottom => (0, -1, height, -1, 0, width),
            SKEncodedOrigin.LeftBottom => (0, 1, 0, -1, 0, width),
            _ => (1, 0, 0, 0, 1, 0)
        };
        return new SKMatrix { ScaleX = a * ratio, SkewX = b * ratio, TransX = tx * ratio,
            SkewY = c * ratio, ScaleY = d * ratio, TransY = ty * ratio, Persp2 = 1 };
    }
    private static bool CanReadReview(MediaAsset asset, Engagement agreement,
        IEnumerable<EngagementReview> reviews, DateTime now) => asset.Purpose == "review" &&
        asset.Stage == "ready" && reviews.FirstOrDefault(x => x.Id == asset.ReviewId) is { } review &&
        IsPublished(review, agreement, now);
    private static bool IsPublished(EngagementReview review, Engagement agreement, DateTime now) =>
        review.WithdrawnAt is null && review.ExcludedAt is null && agreement.OutcomeState == "both_confirmed" &&
        (review.PublishedAt is not null || now >= review.PublishAfterAt);
    private bool IsOperator()
    {
        var group = config["Safety:OperatorGroup"];
        if (string.IsNullOrWhiteSpace(group) || User.Identity?.IsAuthenticated != true) return false;
        return User.FindAll("cognito:groups").Any(claim => claim.Value == group ||
            claim.Value.StartsWith('[') && JsonContainsGroup(claim.Value, group));
    }
    private static bool JsonContainsGroup(string value, string group)
    {
        try { using var json = JsonDocument.Parse(value); return json.RootElement.ValueKind == JsonValueKind.Array &&
            json.RootElement.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && x.GetString() == group); }
        catch (JsonException) { return false; }
    }
}
