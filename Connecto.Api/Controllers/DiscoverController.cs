using Connecto.Api.Authentication;
using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Connecto.Api.Controllers;

[ApiController]
[Route("api/discover")]
public sealed class DiscoverController(ConnectoDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("offerings")]
    public async Task<IActionResult> Offerings([FromQuery] string? query, [FromQuery] string? category,
        [FromQuery] bool availableToday = false, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || query?.Length > 100 || category?.Length > 100)
            return BadRequest(new ProblemDetails { Title = "Invalid discovery filters" });

        var memberId = (await currentUser.GetAsync(cancellationToken)).Id;
        var candidates = db.Offerings.AsNoTracking().Include(x => x.User).Include(x => x.Skills)
            .Where(x => x.UserId != memberId && x.Status == OfferingStatus.Published &&
                x.PricingBasis != PricingBasis.LegacyUnknown && x.Currency == "NZD" &&
                !db.MemberBlocks.Any(b => (b.BlockerId == memberId && b.BlockedId == x.UserId) ||
                    (b.BlockerId == x.UserId && b.BlockedId == memberId)));
        if (!string.IsNullOrWhiteSpace(category))
            candidates = candidates.Where(x => x.Category == category);
        if (availableToday)
        {
            var today = DateTime.UtcNow;
            candidates = candidates.Where(x => x.AvailableFrom != null && x.AvailableFrom <= today &&
                (x.AvailableUntil == null || x.AvailableUntil >= today));
        }
        var terms = string.IsNullOrWhiteSpace(query) ? [] : DiscoveryVocabulary.Expand(query);
        if (terms.Length > 0) candidates = candidates.Where(DiscoveryVocabulary.Predicate(terms));
        var total = await candidates.CountAsync(cancellationToken);
        var offerings = await candidates.OrderBy(x => x.Title).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new { offerings = offerings.Select(DiscoveryOfferProjection.From), total, page, pageSize });
    }

    [HttpGet("offerings/{id:guid}")]
    public async Task<IActionResult> Offering(Guid id, CancellationToken cancellationToken)
    {
        var memberId = (await currentUser.GetAsync(cancellationToken)).Id;
        var offering = await db.Offerings.AsNoTracking().Include(x => x.User).Include(x => x.Skills)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId != memberId &&
                x.Status == OfferingStatus.Published && x.PricingBasis != PricingBasis.LegacyUnknown &&
                x.Currency == "NZD" &&
                !db.MemberBlocks.Any(b => (b.BlockerId == memberId && b.BlockedId == x.UserId) ||
                    (b.BlockerId == x.UserId && b.BlockedId == memberId)), cancellationToken);
        return offering is null ? NotFound() : Ok(DiscoveryOfferProjection.From(offering));
    }
}

public static class DiscoveryVocabulary
{
    // Small, inspectable related-wording set. Pilot acceptance still needs owner-reviewed fixtures.
    private static readonly string[][] Groups = [
        ["gardening", "garden", "weeding", "plants"],
        ["computer", "laptop", "technology", "tech"],
        ["tutoring", "lessons", "learning", "teaching"],
        ["cleaning", "housekeeping", "household"]
    ];

    public static string[] Expand(string query)
    {
        var words = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.SelectMany(word => Groups.FirstOrDefault(group => group.Contains(word, StringComparer.OrdinalIgnoreCase))
            ?? [word]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static Expression<Func<Offering, bool>> Predicate(string[] terms)
    {
        var offering = Expression.Parameter(typeof(Offering), "offering");
        var lower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        Expression? matches = null;
        foreach (var term in terms)
        {
            foreach (var field in new[] { nameof(Offering.Title), nameof(Offering.Description), nameof(Offering.Category) })
            {
                var value = Expression.Call(Expression.Property(offering, field), lower);
                var found = Expression.Call(value, contains, Expression.Constant(term.ToLowerInvariant()));
                matches = matches is null ? found : Expression.OrElse(matches, found);
            }
        }
        return Expression.Lambda<Func<Offering, bool>>(matches ?? Expression.Constant(true), offering);
    }
}
