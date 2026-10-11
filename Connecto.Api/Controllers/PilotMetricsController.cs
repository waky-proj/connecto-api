using System.Text.Json;
using Connecto.Infrastructure.Services.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Api.Controllers;

// A request-created cohort. All counts are distinct durable aggregates, never outbox deliveries.
[ApiController]
[Route("api/pilot/metrics")]
public sealed class PilotMetricsController(ConnectoDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateTime from, [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        if (!HasAccess()) return Forbid();
        if (from.Kind != DateTimeKind.Utc || to.Kind != DateTimeKind.Utc ||
            from >= to || to > DateTime.UtcNow.AddDays(1) || to - from > TimeSpan.FromDays(366))
            return BadRequest(new ProblemDetails { Title = "Use a UTC half-open window of at most 366 days" });

        var requests = await db.ConnectionRequests.AsNoTracking()
            .Where(x => x.CreatedAt >= from && x.CreatedAt < to)
            .Select(x => new { x.Id, x.SenderId, x.RecipientId, x.CreatedAt, x.Status })
            .ToListAsync(cancellationToken);
        var signals = await db.PilotSignals.AsNoTracking()
            .Where(x => x.OccurredAt >= from && x.OccurredAt < to)
            .Select(x => new { x.NeedId, x.OfferingId, x.Kind, x.CandidateCount })
            .ToListAsync(cancellationToken);
        var searchedNeeds = signals.Where(x => x.Kind == "discovery").GroupBy(x => x.NeedId).ToArray();
        var coveredNeeds = searchedNeeds.Where(x => x.Any(s => s.CandidateCount > 0))
            .Select(x => x.Key).ToHashSet();
        var suitableNeeds = signals.Where(x => x.Kind == "suitable" && coveredNeeds.Contains(x.NeedId))
            .Select(x => x.NeedId).Distinct().Count();
        var requestIds = requests.Select(x => x.Id).ToHashSet();
        var agreements = await db.Engagements.AsNoTracking()
            .Where(x => requestIds.Contains(x.ConnectionRequestId))
            .Select(x => new { x.Id, x.ConnectionRequestId, x.RequesterId, x.ProviderId,
                x.AcceptedAt, x.OutcomeState })
            .ToListAsync(cancellationToken);
        var agreementIds = agreements.Select(x => x.Id).ToHashSet();
        var reviews = await db.EngagementReviews.AsNoTracking()
            .Where(x => agreementIds.Contains(x.EngagementId) && x.WithdrawnAt == null && x.ExcludedAt == null)
            .Select(x => new { x.EngagementId, x.ReviewerId, x.ReviewerRole, x.PublishedAt,
                x.Reliability, x.Communication, x.Agreement, x.Clarity, x.Commitments })
            .ToListAsync(cancellationToken);
        var accepted = agreements.Count;
        var completed = agreements.Count(x => x.OutcomeState == "both_confirmed");
        var eligible = agreements.Where(x => x.OutcomeState == "both_confirmed").Select(x => x.Id).ToHashSet();
        var submitted = reviews.Where(x => eligible.Contains(x.EngagementId))
            .Select(x => (x.EngagementId, x.ReviewerId)).Distinct().Count();
        var bilateral = reviews.Where(x => eligible.Contains(x.EngagementId) && x.PublishedAt != null)
            .GroupBy(x => x.EngagementId).Count(x => x.Select(r => r.ReviewerId).Distinct().Count() == 2);
        var ratings = reviews.Where(x => eligible.Contains(x.EngagementId) && x.PublishedAt != null)
            .SelectMany(x => new int?[] { x.Reliability, x.Communication, x.Agreement, x.Clarity, x.Commitments })
            .Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        var requesterRatings = reviews.Where(x => eligible.Contains(x.EngagementId) &&
                x.PublishedAt != null && x.ReviewerRole == "requester")
            .SelectMany(x => new int?[] { x.Reliability, x.Communication, x.Agreement, x.Clarity, x.Commitments })
            .Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        var providerRatings = reviews.Where(x => eligible.Contains(x.EngagementId) &&
                x.PublishedAt != null && x.ReviewerRole == "provider")
            .SelectMany(x => new int?[] { x.Reliability, x.Communication, x.Agreement, x.Clarity, x.Commitments })
            .Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        var connectHours = agreements.Join(requests, a => a.ConnectionRequestId, r => r.Id,
            (a, r) => (a.AcceptedAt - r.CreatedAt).TotalHours).Where(x => x >= 0).Order().ToArray();
        var memberPairs = agreements.SelectMany(x => new[] { x.RequesterId, x.ProviderId })
            .GroupBy(x => x).ToArray();
        return Ok(new
        {
            cohort = new { definition = "connection request created in [from,to) UTC", from, to,
                requestCount = requests.Count, distinctRequesters = requests.Select(x => x.SenderId).Distinct().Count(),
                distinctProviders = requests.Select(x => x.RecipientId).Distinct().Count(),
                source = "durable API records; browser demo is session-only" },
            discovery = new { coveredNeeds = coveredNeeds.Count, searchedNeeds = searchedNeeds.Length,
                definition = "distinct published Needs with an instrumented search in window; covered when any search returned a server-verified eligible candidate" },
            suitability = new { markedNeeds = suitableNeeds, coveredNeeds = coveredNeeds.Count,
                markedNeedOfferingPairs = signals.Where(x => x.Kind == "suitable")
                    .Select(x => (x.NeedId, x.OfferingId)).Distinct().Count(),
                definition = "requester explicitly marked an offering suitable in window; denominator is covered searched Needs" },
            requestStates = new { pending = requests.Count(x => x.Status == "pending"),
                declined = requests.Count(x => x.Status == "declined"),
                withdrawn = requests.Count(x => x.Status == "withdrawn"),
                accepted = requests.Count(x => x.Status == "accepted") },
            acceptance = new { numerator = accepted, denominator = requests.Count },
            completion = new { numerator = completed, denominator = accepted,
                oneReported = agreements.Count(x => x.OutcomeState == "one_reported"),
                cancelled = agreements.Count(x => x.OutcomeState == "cancelled"),
                disputed = agreements.Count(x => x.OutcomeState == "disputed") },
            disagreement = new { numerator = agreements.Count(x => x.OutcomeState == "disputed"), denominator = accepted },
            feedback = new { submittedParticipantSlots = submitted, eligibleParticipantSlots = completed * 2,
                bilateralPublishedAgreements = bilateral, eligibleAgreements = completed,
                publishedRatingCount = ratings.Length, publishedRatingMean = ratings.Length == 0 ? (double?)null : ratings.Average(),
                requesterRatingCount = requesterRatings.Length,
                requesterRatingMean = requesterRatings.Length == 0 ? (double?)null : requesterRatings.Average(),
                providerRatingCount = providerRatings.Length,
                providerRatingMean = providerRatings.Length == 0 ? (double?)null : providerRatings.Average() },
            timeToConnect = new { unit = "hours", count = connectHours.Length,
                median = connectHours.Length == 0 ? (double?)null :
                    (connectHours[(connectHours.Length - 1) / 2] + connectHours[connectHours.Length / 2]) / 2 },
            repeatUse = new { numerator = memberPairs.Count(x => x.Count() > 1),
                denominator = memberPairs.Length, definition = "member appears in at least two accepted agreements in cohort" },
            unavailable = new[] { "pilotTargets: owner decision required", "coverageBias: representative pilot population required" }
        });
    }

    private bool HasAccess() => InGroup(configuration["Safety:OperatorGroup"]) || InGroup(configuration["Pilot:ViewerGroup"]);

    private bool InGroup(string? group)
    {
        if (string.IsNullOrWhiteSpace(group) || User.Identity?.IsAuthenticated != true) return false;
        return User.FindAll("cognito:groups").Any(claim =>
        {
            if (claim.Value == group) return true;
            if (!claim.Value.StartsWith('[')) return false;
            try
            {
                using var document = JsonDocument.Parse(claim.Value);
                return document.RootElement.ValueKind == JsonValueKind.Array &&
                    document.RootElement.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && x.GetString() == group);
            }
            catch (JsonException) { return false; }
        });
    }
}
