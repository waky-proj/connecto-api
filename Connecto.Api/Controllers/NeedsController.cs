using Connecto.Application.Internal;
using Connecto.Domain.Requests;
using Microsoft.AspNetCore.Mvc;
using Connecto.Api.Authentication;
using Connecto.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Connecto.Infrastructure.Services.Data;
using Microsoft.EntityFrameworkCore;
using Connecto.Domain.Entities;

namespace Connecto.Api.Controllers
{
    [Route("api/needs")]
    [ApiController]
    /// <summary>
    /// Handles creation, retrieval, and matching actions for user needs.
    /// </summary>
    public class NeedsController(IMatchService matchService, INeedService needService,
        ICurrentUser currentUser, INeedDbService needs, IAuthorizationService authorization,
        ConnectoDbContext db) : ControllerBase
    {

        [HttpPost]
        public async Task<IActionResult> Post(
            CreateNeedRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var need = await needService.CreateAsync(
                    (await currentUser.GetAsync(cancellationToken)).Id,
                    request,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetMatches),
                    new { needId = need.Id },
                    new { need.Id, need.Title, need.Category, need.Currency, need.PricingBasis,
                        need.EstimatedHours, need.Revision, need.Status });
            }
            catch (ArgumentException error)
            {
                return BadRequest(new ProblemDetails { Title = "Invalid listing terms", Detail = error.Message });
            }
        }

        [HttpGet("{needId:guid}/matches")]
        public async Task<IActionResult> GetMatches(
            Guid needId,
            CancellationToken cancellationToken)
        {
            var need = await needs.GetById(needId);
            if (need is null) return NotFound();
            if (!(await authorization.AuthorizeAsync(User, need, "Owner")).Succeeded) return Forbid();
            var matches = await matchService.FindMatchesAsync(
                needId,
                cancellationToken);

            var ids = matches.Select(x => x.OfferingId).ToArray();
            var offerings = await db.Offerings.AsNoTracking().Include(x => x.User)
                .Include(x => x.Skills).Where(x => ids.Contains(x.Id) &&
                    x.Status == OfferingStatus.Published && x.PricingBasis != PricingBasis.LegacyUnknown &&
                    x.UserId != need.UserId).ToDictionaryAsync(x => x.Id, cancellationToken);
            return Ok(matches.Where(x => offerings.ContainsKey(x.OfferingId)).Select(x => new
            {
                x.NeedId, x.OfferingId, x.Score, x.Reasons, x.Unknowns, x.PolicyVersion,
                Offering = DiscoveryOfferProjection.From(offerings[x.OfferingId])
            }));
        }
    }
}
