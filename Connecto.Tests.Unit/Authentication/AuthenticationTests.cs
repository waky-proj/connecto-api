using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Connecto.Api.Authentication;
using Connecto.Domain.Entities;
using Connecto.Domain.Requests;
using Connecto.Infrastructure.Services.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Connecto.Tests.Unit.Authentication;

public class AuthenticationTests
{
    [Fact]
    public async Task Anonymous_requests_are_rejected_but_health_is_public()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/needs", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/offerings", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Valid_access_token_provisions_one_internal_user_across_requests()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        var first = await client.GetFromJsonAsync<Profile>("/api/users/me");
        var second = await client.GetFromJsonAsync<Profile>("/api/users/me");
        Assert.NotEqual(Guid.Empty, first!.Id);
        Assert.Equal(first.Id, second!.Id);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
        Assert.Single(await db.Users.Where(x => x.AuthSubjectId == "subject-1").ToListAsync());
    }

    [Theory]
    [InlineData("id", "test-client", "https://test-issuer.example", false, false)]
    [InlineData("access", "wrong-client", "https://test-issuer.example", false, false)]
    [InlineData("access", "test-client", "https://wrong-issuer.example", false, false)]
    [InlineData("access", "test-client", "https://test-issuer.example", true, false)]
    [InlineData("access", "test-client", "https://test-issuer.example", false, true)]
    public async Task Invalid_tokens_are_rejected(string use, string clientId, string issuer, bool expired, bool wrongKey)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(use, clientId, issuer, expired, wrongKey));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Ownership_policy_accepts_owner_and_rejects_other_users_for_both_listing_types()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        var profile = await client.GetFromJsonAsync<Profile>("/api/users/me");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
        var current = new FixedCurrentUser(await db.Users.SingleAsync(x => x.Id == profile!.Id));
        var handler = new OwnershipAuthorization(current);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "subject-1")], "test"));
        foreach (var resource in new object[] { new Need { UserId = profile!.Id }, new Offering { UserId = profile.Id } })
        {
            var context = new AuthorizationHandlerContext([new OwnerRequirement()], principal, resource);
            await handler.HandleAsync(context);
            Assert.True(context.HasSucceeded);
        }
        foreach (var resource in new object[] { new Need { UserId = Guid.NewGuid() }, new Offering { UserId = Guid.NewGuid() } })
        {
            var context = new AuthorizationHandlerContext([new OwnerRequirement()], principal, resource);
            await handler.HandleAsync(context);
            Assert.False(context.HasSucceeded);
        }
    }

    private record Profile(Guid Id);
    private record Listing(Guid Id);

    [Fact]
    public async Task Listing_creation_ignores_spoofed_user_header_and_other_users_cannot_read_matches()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());
        var needResponse = await client.PostAsJsonAsync("/api/needs", new CreateNeedRequest("Need", "Description", "Technical", ["Programming"], null, null, null, null));
        Assert.Equal(HttpStatusCode.Created, needResponse.StatusCode);
        var need = await needResponse.Content.ReadFromJsonAsync<Listing>();
        var offeringResponse = await client.PostAsJsonAsync("/api/offerings", new CreateOfferingRequest("Offering", "Description", "Technical", ["Programming"], null, null, "Auckland", false, null, null, 0));
        Assert.Equal(HttpStatusCode.Created, offeringResponse.StatusCode);
        var offering = await offeringResponse.Content.ReadFromJsonAsync<Listing>();
        var profile = await client.GetFromJsonAsync<Profile>("/api/users/me");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            Assert.Equal(profile!.Id, (await db.Needs.FindAsync(need!.Id))!.UserId);
            Assert.Equal(profile.Id, (await db.Offerings.FindAsync(offering!.Id))!.UserId);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "subject-2"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/needs/{need!.Id}/matches")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/offerings/{offering!.Id}/matches")).StatusCode);
    }
    private sealed class FixedCurrentUser(User user) : ICurrentUser
    {
        public Task<User> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(user);
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly RSA rsa = RSA.Create(2048);
        private readonly string database = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Cognito:Issuer", "https://test-issuer.example");
            builder.UseSetting("Cognito:Audience", "test-client");
            builder.UseSetting("OpenAI:ApiKey", "test-key");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ConnectoDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ConnectoDbContext>>();
                services.AddDbContext<ConnectoDbContext>(options => options.UseInMemoryDatabase(database));
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var configuration = new OpenIdConnectConfiguration { Issuer = "https://test-issuer.example" };
                    configuration.SigningKeys.Add(new RsaSecurityKey(rsa) { KeyId = "test-key" });
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
            });
        }

        public string Token(string use = "access", string clientId = "test-client", string issuer = "https://test-issuer.example", bool expired = false, bool wrongKey = false, string subject = "subject-1")
        {
            using var other = RSA.Create(2048);
            var key = new RsaSecurityKey(wrongKey ? other : rsa) { KeyId = "test-key" };
            var token = new JwtSecurityToken(issuer, claims: [new Claim("sub", subject), new Claim("token_use", use), new Claim("client_id", clientId)],
                notBefore: DateTime.UtcNow.AddHours(-2), expires: expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) rsa.Dispose();
        }
    }
}
