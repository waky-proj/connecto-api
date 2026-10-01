using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Api.Authentication;

public interface ICurrentUser
{
    Task<User> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class CurrentUser(IHttpContextAccessor accessor, ConnectoDbContext db) : ICurrentUser
{
    public async Task<User> GetAsync(CancellationToken cancellationToken = default)
    {
        var principal = accessor.HttpContext?.User;
        var subject = principal?.FindFirst("sub")?.Value;
        if (principal?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(subject))
            throw new UnauthorizedAccessException("An authenticated subject is required.");
        var existing = await db.Users.SingleOrDefaultAsync(x => x.AuthSubjectId == subject, cancellationToken);
        if (existing is not null) return existing;
        // Access tokens normally omit profile attributes; profile editing is separate.
        var user = new User("", "", "", "", "", "", "", "") { AuthSubjectId = subject };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return user;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(user).State = EntityState.Detached;
            return await db.Users.SingleAsync(x => x.AuthSubjectId == subject, cancellationToken);
        }
    }
}
