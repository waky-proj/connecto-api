using Connecto.Domain.Entities;
using Connecto.Domain.Listings;
using Connecto.Domain.Requests;
using Connecto.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Api.Controllers;

[ApiController]
public sealed class ListingEditController(
    INeedDbService needs,
    IOfferingDbService offerings,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpPatch("/api/needs/{id:guid}")]
    public Task<IActionResult> UpdateNeed(Guid id, UpdateNeedRequest request) =>
        UpdateNeedCore(id, request);

    [HttpPatch("/api/offerings/{id:guid}")]
    public Task<IActionResult> UpdateOffering(Guid id, UpdateOfferingRequest request) =>
        UpdateOfferingCore(id, request);

    private async Task<IActionResult> UpdateNeedCore(Guid id, UpdateNeedRequest request)
    {
        var need = await needs.GetById(id);
        if (need is null) return NotFound();
        if (!(await authorization.AuthorizeAsync(User, need, "Owner")).Succeeded) return Forbid();
        if (need.Revision != request.Revision) return Conflict(new { title = "Listing revision conflict" });
        if (need.Status == NeedStatus.Closed) return Conflict(new { title = "Listing state conflict" });
        try
        {
            Validate(request.Title, request.Description, request.Category, request.Currency, request.PricingBasis,
                request.BudgetMin, request.BudgetMax, request.EstimatedHours, true);
            need.Title = request.Title.Trim(); need.Description = request.Description.Trim(); need.Category = request.Category.Trim();
            need.BudgetMin = request.BudgetMin; need.BudgetMax = request.BudgetMax; need.Currency = ListingPolicy.Currency;
            need.PricingBasis = ListingPolicy.ParsePricingBasis(request.PricingBasis); need.EstimatedHours = request.EstimatedHours;
            need.Location = request.Location?.Trim() ?? ""; need.Deadline = request.Deadline; need.Revision++;
            need.Skills.Clear();
            foreach (var skill in request.Skills.Select(ParseSkill).Where(skill => skill != Skill.None))
                need.Skills.Add(new NeedSkill { Id = Guid.NewGuid(), NeedId = need.Id, Skill = skill });
            if (!await needs.Update(need, request.Revision)) return Conflict(new { title = "Listing revision conflict" });
            return Ok(new { need.Id, need.Title, need.Category, need.Currency, need.PricingBasis, need.EstimatedHours, need.Revision, need.Status });
        }
        catch (ArgumentException error) { return BadRequest(new ProblemDetails { Title = "Invalid listing terms", Detail = error.Message }); }
    }

    private async Task<IActionResult> UpdateOfferingCore(Guid id, UpdateOfferingRequest request)
    {
        var offering = await offerings.GetById(id);
        if (offering is null) return NotFound();
        if (!(await authorization.AuthorizeAsync(User, offering, "Owner")).Succeeded) return Forbid();
        if (offering.Revision != request.Revision) return Conflict(new { title = "Listing revision conflict" });
        if (offering.Status == OfferingStatus.Closed) return Conflict(new { title = "Listing state conflict" });
        try
        {
            if (request.ExperienceYears < 0) throw new ArgumentException("ExperienceYears cannot be negative.");
            Validate(request.Title, request.Description, request.Category, request.Currency, request.PricingBasis,
                request.PriceMin, request.PriceMax, request.EstimatedHours, false);
            offering.Title = request.Title.Trim(); offering.Description = request.Description.Trim(); offering.Category = request.Category.Trim();
            offering.PriceMin = request.PriceMin; offering.PriceMax = request.PriceMax; offering.Currency = ListingPolicy.Currency;
            offering.PricingBasis = ListingPolicy.ParsePricingBasis(request.PricingBasis); offering.EstimatedHours = request.EstimatedHours;
            offering.Location = request.Location?.Trim(); offering.RemoteAvailable = request.RemoteAvailable;
            offering.AvailableFrom = request.AvailableFrom; offering.AvailableUntil = request.AvailableUntil;
            offering.ExperienceYears = request.ExperienceYears; offering.Revision++;
            offering.Skills.Clear();
            foreach (var skill in request.Skills.Select(ParseSkill).Where(skill => skill != Skill.None))
                offering.Skills.Add(new OfferingSkill { Id = Guid.NewGuid(), OfferingId = offering.Id, Skill = skill });
            if (!await offerings.Update(offering, request.Revision)) return Conflict(new { title = "Listing revision conflict" });
            return Ok(new { offering.Id, offering.Title, offering.Category, offering.Currency, offering.PricingBasis, offering.EstimatedHours, offering.Revision, offering.Status });
        }
        catch (ArgumentException error) { return BadRequest(new ProblemDetails { Title = "Invalid listing terms", Detail = error.Message }); }
    }

    private static void Validate(string title, string description, string category, string? currency, string? pricingBasis,
        decimal? minimum, decimal? maximum, decimal? estimatedHours, bool needsHours)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.");
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.");
        if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Category is required.");
        ListingPolicy.ValidateTerms(category.Trim(), currency, ListingPolicy.ParsePricingBasis(pricingBasis),
            minimum, maximum, estimatedHours, needsHours);
    }

    private static Skill ParseSkill(string value) => Enum.TryParse<Skill>(value, out var skill) ? skill : Skill.None;
}
