using Connecto.Application.Internal.AI;
using Connecto.Domain.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Connecto.Api.Controllers
{
    [ApiController]
    [Route("api/ai/parse")]
    /// <summary>
    /// Exposes endpoints for parsing listing content into structured marketplace data.
    /// </summary>
    public sealed class ListingParseController(
            IListingParseService service) : ControllerBase
    {
        [HttpPost("need")]
        public async Task<IActionResult> ParseNeed(
            ParseListingRequest request,
            CancellationToken cancellationToken)
        {
            var result = await service.ParseNeedAsync(
                request.Title,
                request.Description,
                cancellationToken);

            return Ok(result);
        }

        [HttpPost("offering")]
        public async Task<IActionResult> ParseOffering(
            ParseListingRequest request,
            CancellationToken cancellationToken)
        {
            var result = await service.ParseOfferingAsync(
                request.Title,
                request.Description,
                cancellationToken);

            return Ok(result);
        }
    }
}
