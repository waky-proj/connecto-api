using Connecto.Api.Authentication;
using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Api.Controllers;

[ApiController]
public sealed class ListingLifecycleController(
    INeedDbService needs,
    IOfferingDbService offerings,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpPost("/api/needs/{id:guid}/pause")]
    public Task<IActionResult> PauseNeed(Guid id, ListingLifecycleRequest request) =>
        ChangeNeed(id, request, NeedStatus.Paused, [NeedStatus.Published]);

    [HttpPost("/api/needs/{id:guid}/close")]
    public Task<IActionResult> CloseNeed(Guid id, ListingLifecycleRequest request) =>
        ChangeNeed(id, request, NeedStatus.Closed, [NeedStatus.Published, NeedStatus.Paused]);

    [HttpPost("/api/offerings/{id:guid}/pause")]
    public Task<IActionResult> PauseOffering(Guid id, ListingLifecycleRequest request) =>
        ChangeOffering(id, request, OfferingStatus.Paused, [OfferingStatus.Published]);

    [HttpPost("/api/offerings/{id:guid}/close")]
    public Task<IActionResult> CloseOffering(Guid id, ListingLifecycleRequest request) =>
        ChangeOffering(id, request, OfferingStatus.Closed, [OfferingStatus.Published, OfferingStatus.Paused]);

    private async Task<IActionResult> ChangeNeed(Guid id, ListingLifecycleRequest request,
        NeedStatus target, IReadOnlyCollection<NeedStatus> permitted)
    {
        var need = await needs.GetById(id);
        if (need is null) return NotFound();
        if (!(await authorization.AuthorizeAsync(User, need, "Owner")).Succeeded) return Forbid();
        if (need.Revision != request.Revision) return Conflict(new { title = "Listing revision conflict" });
        if (!permitted.Contains(need.Status)) return Conflict(new { title = "Listing state conflict" });
        need.Status = target;
        need.Revision++;
        try { await needs.Update(need); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { title = "Listing revision conflict" }); }
        return Ok(new { need.Id, need.Status, need.Revision });
    }

    private async Task<IActionResult> ChangeOffering(Guid id, ListingLifecycleRequest request,
        OfferingStatus target, IReadOnlyCollection<OfferingStatus> permitted)
    {
        var offering = await offerings.GetById(id);
        if (offering is null) return NotFound();
        if (!(await authorization.AuthorizeAsync(User, offering, "Owner")).Succeeded) return Forbid();
        if (offering.Revision != request.Revision) return Conflict(new { title = "Listing revision conflict" });
        if (!permitted.Contains(offering.Status)) return Conflict(new { title = "Listing state conflict" });
        offering.Status = target;
        offering.Revision++;
        try { await offerings.Update(offering); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { title = "Listing revision conflict" }); }
        return Ok(new { offering.Id, offering.Status, offering.Revision });
    }
}

public sealed record ListingLifecycleRequest(int Revision);
