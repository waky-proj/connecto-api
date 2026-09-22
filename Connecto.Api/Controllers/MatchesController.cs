using Connecto.Domain.Matching;
using Connecto.Domain.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Connecto.Api.Controllers
{
    [ApiController]
    [Route("api/matches")]
    public class MatchesController : ControllerBase
    {
        [HttpGet("preview")]
        public async Task<IActionResult> Preview()
        {
            return Ok();
        }

        private static MatchResponse ToResponse(MatchResult result)
            => new(
                result.OfferingId,
                Math.Round(result.Score.Total, 2),
                new MatchBreakdown(
                    result.Score.Skills,
                    result.Score.Requirements,
                    result.Score.Availability,
                    result.Score.Budget,
                    result.Score.Location,
                    result.Score.Experience,
                    result.Score.Reputation),
                result.Reasons);
    }
}
