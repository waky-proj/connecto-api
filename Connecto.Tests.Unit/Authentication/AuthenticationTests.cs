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
    [Theory]
    [InlineData("")]
    [InlineData("configured-but-unimplemented")]
    public async Task Unavailable_parsing_does_not_block_structured_marketplace(string key)
    {
        using var factory = new ApiFactory(key);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        foreach (var route in new[] { "/api/ai/parse/need", "/api/ai/parse/offering" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(route, new { title = "Need", description = "Help" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        foreach (var route in new[] { "/api/ai/parse/need", "/api/ai/parse/offering" })
        {
            var response = await client.PostAsJsonAsync(route, new { title = "Need", description = "Help" });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
            Assert.Equal("Listing parsing unavailable", problem!.Title);
            Assert.Contains("manually", problem.Detail!);
            Assert.DoesNotContain(key.Length > 0 ? key : "design", await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync("/api/users/me", new { city = "Auckland" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/needs",
            new CreateNeedRequest("Need", "Help", "Technical", ["Programming"], null, null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/offerings",
            new CreateOfferingRequest("Offering", "Help", "Technical", ["Programming"], null, null, "Auckland", false, null, null, 0))).StatusCode);
        var activity = await client.GetFromJsonAsync<Activity>("/api/users/me/activity");
        Assert.Single(activity!.Needs);
        Assert.Single(activity.Offerings);
    }

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
    [Fact]
    public async Task Profile_is_retrieved_by_fresh_client_and_other_member_edits_are_isolated()
    {
        using var factory = new ApiFactory();
        using var first = factory.CreateClient();
        first.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        Assert.Equal(HttpStatusCode.OK, (await first.PatchAsJsonAsync("/api/users/me", new {
            firstName = "Mary Jane", lastName = "van der Berg", city = "Auckland", bio = "Saved profile" })).StatusCode);

        using var other = factory.CreateClient();
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "subject-2"));
        Assert.Equal(HttpStatusCode.OK, (await other.PatchAsJsonAsync("/api/users/me", new {
            firstName = "Other", city = "Wellington", bio = "Private B" })).StatusCode);

        using var fresh = factory.CreateClient();
        fresh.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        var profile = await fresh.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/users/me");
        Assert.Equal("Mary Jane", profile.GetProperty("firstName").GetString());
        Assert.Equal("van der Berg", profile.GetProperty("lastName").GetString());
        Assert.Equal("Auckland", profile.GetProperty("city").GetString());
        Assert.Equal("Saved profile", profile.GetProperty("bio").GetString());
        Assert.False(profile.TryGetProperty("authSubjectId", out _));
        Assert.Equal(HttpStatusCode.OK, (await fresh.PatchAsJsonAsync("/api/users/me", new { bio = "" })).StatusCode);
        var cleared = await fresh.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/users/me");
        Assert.Equal("", cleared.GetProperty("bio").GetString());
        Assert.Equal("Mary Jane", cleared.GetProperty("firstName").GetString());
    }

    [Theory]
    [InlineData("firstName", 201)]
    [InlineData("lastName", 201)]
    [InlineData("city", 201)]
    [InlineData("bio", 2001)]
    public async Task Invalid_profile_lengths_do_not_mutate_saved_data(string field, int length)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        await client.PatchAsJsonAsync("/api/users/me", new { firstName = "Saved", city = "Auckland", bio = "Original" });
        var payload = new Dictionary<string, string> { [field] = new string('x', length) };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync("/api/users/me", payload)).StatusCode);
        var profile = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/users/me");
        Assert.Equal("Saved", profile.GetProperty("firstName").GetString());
        Assert.Equal("Auckland", profile.GetProperty("city").GetString());
        Assert.Equal("Original", profile.GetProperty("bio").GetString());
    }

    [Fact]
    public async Task Profile_patch_is_persistent_and_cannot_assign_identity_or_reputation()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        var profile = await client.GetFromJsonAsync<Profile>("/api/users/me");
        var response = await client.PatchAsJsonAsync("/api/users/me", new {
            firstName = " Alice ", city = " Auckland ", bio = "Hello", id = Guid.NewGuid(),
            authSubjectId = "subject-2", reputationScore = 100, email = "spoof@example.com" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<ConnectoDbContext>().Users.SingleAsync(x => x.Id == profile!.Id);
            Assert.Equal("Alice", user.FirstName);
            Assert.Equal("Auckland", user.City);
            Assert.Equal("subject-1", user.AuthSubjectId);
            Assert.Equal("", user.Email);
            Assert.Equal(0, user.ReputationScore);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync("/api/users/me", new { bio = new string('x', 2001) })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "subject-2"));
        var other = await client.GetFromJsonAsync<Profile>("/api/users/me");
        Assert.NotEqual(profile!.Id, other!.Id);
    }

    [Fact]
    public async Task Activity_is_paginated_and_never_returns_another_members_listings()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        var response = await client.PostAsJsonAsync("/api/needs", new CreateNeedRequest("Private A", "Description", "Technical", ["Programming"], 1, 10, "Auckland", null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var own = await client.GetFromJsonAsync<Activity>("/api/users/me/activity?pageSize=1");
        Assert.Equal(1, own!.NeedTotal);
        Assert.Single(own.Needs);
        var next = await client.GetFromJsonAsync<Activity>("/api/users/me/activity?pageSize=1&page=2");
        Assert.Empty(next!.Needs);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/users/me/activity?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/users/me/activity?page=0")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "subject-2"));
        var other = await client.GetFromJsonAsync<Activity>("/api/users/me/activity");
        Assert.Empty(other!.Needs);
        Assert.Empty(other.Offerings);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me/activity")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PatchAsJsonAsync("/api/users/me", new { city = "Spoof" })).StatusCode);
    }

    private record Activity(int NeedTotal, Listing[] Needs, Listing[] Offerings);
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

    private sealed class ApiFactory(string apiKey = "") : WebApplicationFactory<Program>
    {
        private readonly RSA rsa = RSA.Create(2048);
        private readonly string database = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Cognito:Issuer", "https://test-issuer.example");
            builder.UseSetting("Cognito:Audience", "test-client");
            builder.UseSetting("OpenAI:ApiKey", apiKey);
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
