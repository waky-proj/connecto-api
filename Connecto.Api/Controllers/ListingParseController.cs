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
            try
            {
                var result = await service.ParseNeedAsync(request.Title, request.Description, cancellationToken);
                return Ok(result);
            }
            catch (ListingParsingUnavailableException)
            {
                return ParsingUnavailable();
            }
        }

        [HttpPost("offering")]
        public async Task<IActionResult> ParseOffering(
            ParseListingRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await service.ParseOfferingAsync(request.Title, request.Description, cancellationToken);
                return Ok(result);
            }
            catch (ListingParsingUnavailableException)
            {
                return ParsingUnavailable();
            }
        }

        private ObjectResult ParsingUnavailable() => Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Listing parsing unavailable",
            detail: "AI listing parsing is not available. Enter the listing fields manually to continue.");
    }
}
