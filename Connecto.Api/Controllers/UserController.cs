using Connecto.Api.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Connecto.Api.Controllers;

[Route("api/users")]
[ApiController]
public class UserController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var user = await currentUser.GetAsync(cancellationToken);
        return Ok(new { user.Id, user.FirstName, user.MiddleName, user.LastName, user.Email,
            user.Address, user.City, user.Country, user.Bio, user.VerificationStatus, user.ReputationScore });
    }
}
