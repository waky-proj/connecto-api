using Connecto.Api.Authentication;
using Microsoft.AspNetCore.Mvc;
using Connecto.Infrastructure.Services.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Connecto.Api.Controllers;

[Route("api/users")]
[ApiController]
public class UserController(ICurrentUser currentUser, ConnectoDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var user = await currentUser.GetAsync(cancellationToken);
        return Ok(new { user.Id, user.FirstName, user.MiddleName, user.LastName, user.Email,
            user.Address, user.City, user.Country, user.Bio, user.VerificationStatus, user.ReputationScore });
    }

    [HttpPatch("me")]
    public async Task<IActionResult> Update(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await currentUser.GetAsync(cancellationToken);
        if (request.FirstName is not null) user.FirstName = request.FirstName.Trim();
        if (request.LastName is not null) user.LastName = request.LastName.Trim();
        if (request.City is not null) user.City = request.City.Trim();
        if (request.Bio is not null) user.Bio = request.Bio.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return await Get(cancellationToken);
    }

    [HttpGet("me/activity")]
    public async Task<IActionResult> Activity([FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var memberId = (await currentUser.GetAsync(cancellationToken)).Id;
        var skip = ((long)page - 1) * pageSize;
        if (skip > int.MaxValue) return BadRequest("Page is out of range.");
        var needs = db.Needs.AsNoTracking().Where(x => x.UserId == memberId);
        var offerings = db.Offerings.AsNoTracking().Where(x => x.UserId == memberId);
        var needTotal = await needs.CountAsync(cancellationToken);
        var offeringTotal = await offerings.CountAsync(cancellationToken);
        var needItems = await needs.OrderBy(x => x.Id).Skip((int)skip).Take(pageSize)
            .Select(x => new { x.Id, x.Title, x.Description, x.Category, x.BudgetMin, x.BudgetMax,
                x.Currency, x.PricingBasis, x.EstimatedHours, x.Revision, x.Location, x.Deadline, x.Status,
                Skills = x.Skills.Select(s => s.Skill).ToArray() })
            .ToListAsync(cancellationToken);
        var offeringItems = await offerings.OrderBy(x => x.Id).Skip((int)skip).Take(pageSize)
            .Select(x => new { x.Id, x.Title, x.Description, x.Category, x.PriceMin, x.PriceMax,
                x.Currency, x.PricingBasis, x.EstimatedHours, x.Revision, x.Location, x.AvailableFrom,
                x.AvailableUntil, x.RemoteAvailable, x.ExperienceYears, x.Status,
                Skills = x.Skills.Select(s => s.Skill).ToArray() })
            .ToListAsync(cancellationToken);
        return Ok(new { page, pageSize, needTotal, offeringTotal, needs = needItems, offerings = offeringItems });
    }
}

public sealed record UpdateProfileRequest(
    [StringLength(200)] string? FirstName,
    [StringLength(200)] string? LastName,
    [StringLength(200)] string? City,
    [StringLength(2000)] string? Bio);
