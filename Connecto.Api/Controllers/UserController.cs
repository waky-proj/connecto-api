using Microsoft.AspNetCore.Mvc;

namespace Connecto.Api.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : ControllerBase
    {
        [HttpPost("create")]
        public async Task<IActionResult> Post()
        {
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {

            return Ok();
        }

        [HttpPost("update")]
        public async Task<IActionResult> Put()
        {

            return Ok();
        }
    }
}
