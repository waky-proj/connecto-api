using Connecto.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Connecto.Api.Authentication;

public sealed class OwnerRequirement : IAuthorizationRequirement;
public sealed class OwnershipAuthorization(ICurrentUser currentUser) : AuthorizationHandler<OwnerRequirement, object>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OwnerRequirement requirement, object resource)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var ownerId = resource switch { Need n => n.UserId, Offering o => o.UserId, _ => (Guid?)null };
        if (ownerId is not null && ownerId == (await currentUser.GetAsync()).Id) context.Succeed(requirement);
    }
}
