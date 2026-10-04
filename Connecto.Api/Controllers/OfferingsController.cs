using Connecto.Application.Internal;
using Connecto.Application.Internal.Impl;
using Connecto.Domain.Requests;
using Microsoft.AspNetCore.Mvc;
using Connecto.Api.Authentication;
using Connecto.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;

namespace Connecto.Api.Controllers
{
    [Route("api/offerings")]
    [ApiController]
    /// <summary>
    /// Handles creation, retrieval, and matching flows for service offerings.
    /// </summary>
    public class OfferingsController(
        IOfferingService offeringService, ICurrentUser currentUser,
        IOfferingDbService offerings, IAuthorizationService authorization) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post(
            CreateOfferingRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var offering = await offeringService.CreateAsync(
                    (await currentUser.GetAsync(cancellationToken)).Id,
                    request,
                    cancellationToken);

                return Created(
                    $"/api/offerings/{offering.Id}",
                    new { offering.Id, offering.Title, offering.Category, offering.Currency,
                        offering.PricingBasis, offering.EstimatedHours, offering.Revision, offering.Status });
            }
            catch (ArgumentException error)
            {
                return BadRequest(new ProblemDetails { Title = "Invalid listing terms", Detail = error.Message });
            }
        }

        [HttpGet("{id:guid}/matches")]
        public async Task<IActionResult> Get(Guid id)
        {
            var offering = await offerings.GetById(id);
            if (offering is null) return NotFound();
            if (!(await authorization.AuthorizeAsync(User, offering, "Owner")).Succeeded) return Forbid();
            return Ok();
        }
    }
}
