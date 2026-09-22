using Connecto.Application.Internal;
using Connecto.Domain.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Connecto.Api.Controllers
{
    [Route("api/needs")]
    [ApiController]
    public class NeedsController(IMatchService matchService, INeedService needService) : ControllerBase
    {

        [HttpPost]
        public async Task<IActionResult> Post(
            [FromHeader(Name = "X-User-Id")] Guid userId,
            CreateNeedRequest request,
            CancellationToken cancellationToken)
        {
            var need = await needService.CreateAsync(
                userId,
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetMatches),
                new { needId = need.Id },
                new
                {
                    need.Id,
                    need.Title,
                    need.Category,
                    need.Status
                });
        }

        [HttpGet("{needId:guid}/matches")]
        public async Task<IActionResult> GetMatches(
            Guid needId,
            CancellationToken cancellationToken)
        {
            var matches = await matchService.FindMatchesAsync(
                needId,
                cancellationToken);

            return Ok(matches);
        }
    }
}
