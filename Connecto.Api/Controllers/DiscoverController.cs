using Microsoft.AspNetCore.Mvc;

namespace Connecto.Api.Controllers
{
    [Route("api/discover")]
    [ApiController]
    /// <summary>
    /// Provides discovery endpoints for exploring potential need and offering matches.
    /// </summary>
    public class DiscoverController : ControllerBase
    {
        [HttpPost("needs")]
        public async Task<IActionResult> Post()
        {
            return Ok();
        }
    }
}
