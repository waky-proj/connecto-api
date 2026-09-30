using Connecto.Application.Internal;
using Connecto.Domain.Requests;
using Microsoft.AspNetCore.Mvc;
using Connecto.Api.Authentication;
using Connecto.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;

namespace Connecto.Api.Controllers
{
    [Route("api/needs")]
    [ApiController]
    /// <summary>
    /// Handles creation, retrieval, and matching actions for user needs.
    /// </summary>
    public class NeedsController(IMatchService matchService, INeedService needService,
        ICurrentUser currentUser, INeedDbService needs, IAuthorizationService authorization) : ControllerBase
    {

        [HttpPost]
        public async Task<IActionResult> Post(
            CreateNeedRequest request,
            CancellationToken cancellationToken)
        {
            var need = await needService.CreateAsync(
                (await currentUser.GetAsync(cancellationToken)).Id,
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
            var need = await needs.GetById(needId);
            if (need is null) return NotFound();
            if (!(await authorization.AuthorizeAsync(User, need, "Owner")).Succeeded) return Forbid();
            var matches = await matchService.FindMatchesAsync(
                needId,
                cancellationToken);

            return Ok(matches);
        }
    }
}
