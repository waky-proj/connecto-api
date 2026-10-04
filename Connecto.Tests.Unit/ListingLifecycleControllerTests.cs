using System.Security.Claims;
using Connecto.Api.Controllers;
using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connecto.Tests.Unit;

public sealed class ListingLifecycleControllerTests
{
    [Fact]
    public async Task Pause_need_changes_a_published_listing_and_increments_its_revision()
    {
        var need = new Need { Id = Guid.NewGuid(), Status = NeedStatus.Published, Revision = 4 };
        var store = new NeedStore(need);
        var controller = CreateController(store, new OfferingStore(), allow: true);

        var result = await controller.PauseNeed(need.Id, new ListingLifecycleRequest(4));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(NeedStatus.Paused, need.Status);
        Assert.Equal(5, need.Revision);
        Assert.True(store.Updated);
    }

    [Fact]
    public async Task Pause_need_rejects_a_stale_revision_without_updating()
    {
        var need = new Need { Id = Guid.NewGuid(), Status = NeedStatus.Published, Revision = 4 };
        var store = new NeedStore(need);
        var controller = CreateController(store, new OfferingStore(), allow: true);

        var result = await controller.PauseNeed(need.Id, new ListingLifecycleRequest(3));

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(NeedStatus.Published, need.Status);
        Assert.Equal(4, need.Revision);
        Assert.False(store.Updated);
    }

    [Fact]
    public async Task Close_offering_rejects_a_non_owner_without_updating()
    {
        var offering = new Offering { Id = Guid.NewGuid(), Status = OfferingStatus.Published, Revision = 1 };
        var store = new OfferingStore(offering);
        var controller = CreateController(new NeedStore(), store, allow: false);

        var result = await controller.CloseOffering(offering.Id, new ListingLifecycleRequest(1));

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(OfferingStatus.Published, offering.Status);
        Assert.Equal(1, offering.Revision);
        Assert.False(store.Updated);
    }

    [Fact]
    public async Task Closed_need_cannot_be_reopened_through_pause()
    {
        var need = new Need { Id = Guid.NewGuid(), Status = NeedStatus.Closed, Revision = 2 };
        var store = new NeedStore(need);
        var controller = CreateController(store, new OfferingStore(), allow: true);

        var result = await controller.PauseNeed(need.Id, new ListingLifecycleRequest(2));

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(NeedStatus.Closed, need.Status);
        Assert.Equal(2, need.Revision);
        Assert.False(store.Updated);
    }

    private static ListingLifecycleController CreateController(INeedDbService needs, IOfferingDbService offerings, bool allow) =>
        new(needs, offerings, new OwnerAuthorization(allow))
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };

    private sealed class OwnerAuthorization(bool allow) : IAuthorizationService
    {
        private readonly AuthorizationResult _result = allow ? AuthorizationResult.Success() : AuthorizationResult.Failed();

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource,
            IEnumerable<IAuthorizationRequirement> requirements) => Task.FromResult(_result);

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource,
            string policyName) => Task.FromResult(_result);
    }

    private sealed class NeedStore(Need? need = null) : INeedDbService
    {
        private readonly Need? _need = need;
        public bool Updated { get; private set; }
        public Task Add(Need item) => Task.CompletedTask;
        public Task<Need?> GetById(Guid id) => Task.FromResult(_need?.Id == id ? _need : null);
        public Task<List<Need>> GetAll() => Task.FromResult(_need is null ? new List<Need>() : new List<Need> { _need });
        public Task Update(Need item) { Updated = true; return Task.CompletedTask; }
        public Task Delete(Need item) => Task.CompletedTask;
    }

    private sealed class OfferingStore(Offering? offering = null) : IOfferingDbService
    {
        private readonly Offering? _offering = offering;
        public bool Updated { get; private set; }
        public Task Add(Offering item) => Task.CompletedTask;
        public Task<Offering?> GetById(Guid id) => Task.FromResult(_offering?.Id == id ? _offering : null);
        public Task<List<Offering>> GetAll() => Task.FromResult(_offering is null ? new List<Offering>() : new List<Offering> { _offering });
        public IQueryable<Offering> Query() => _offering is null ? Enumerable.Empty<Offering>().AsQueryable() : new[] { _offering }.AsQueryable();
        public Task Update(Offering item) { Updated = true; return Task.CompletedTask; }
        public Task Delete(Offering item) => Task.CompletedTask;
    }
}
