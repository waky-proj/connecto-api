using Connecto.Application.Internal;
using Connecto.Application.Internal.Impl;
using Connecto.Domain.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Connecto.Api.Controllers
{
    [Route("api/offerings")]
    [ApiController]
    /// <summary>
    /// Handles creation, retrieval, and matching flows for service offerings.
    /// </summary>
    public class OfferingsController(
        IOfferingService offeringService) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post(
            [FromHeader(Name = "X-User-Id")] Guid userId,
            CreateOfferingRequest request,
            CancellationToken cancellationToken)
        {
            var offering = await offeringService.CreateAsync(
                userId,
                request,
                cancellationToken);

            return Created(
                $"/api/offerings/{offering.Id}",
                new
                {
                    offering.Id,
                    offering.Title,
                    offering.Category,
                    offering.Status
                });
        }

        [HttpGet("/{id}/matches")]
        public async Task<IActionResult> Get(Guid id)
        {

            return Ok();
        }
    }
}
