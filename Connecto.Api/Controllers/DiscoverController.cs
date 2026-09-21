using Microsoft.AspNetCore.Mvc;

namespace Connecto.Api.Controllers
{
    [Route("api/discover")]
    [ApiController]
    public class DiscoverController : ControllerBase
    {
        [HttpPost("needs")]
        public async Task<IActionResult> Post()
        {
            return Ok();
        }
    }
}
