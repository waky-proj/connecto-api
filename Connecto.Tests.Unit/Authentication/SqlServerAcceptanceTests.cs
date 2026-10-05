using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Connecto.Infrastructure.Services.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

public class SqlServerAcceptanceTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerAcceptance")]
    public async Task Profile_ownership_activity_and_fresh_api_host_use_sql_server()
    {
        var connection = Environment.GetEnvironmentVariable("CONNECTO_F3_SQL_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        Assert.Equal("YES", Environment.GetEnvironmentVariable("CONNECTO_F3_ALLOW_DATABASE_WRITES"));

        var suffix = Guid.NewGuid().ToString("N");
        var subjectA = $"f3-a-{suffix}";
        var subjectB = $"f3-b-{suffix}";
        using var rsa = RSA.Create(2048);
        var schemaReady = false;
        try
        {
            using (var factory = new SqlFactory(connection, rsa))
            {
                await RequireCurrentSchema(factory);
                schemaReady = true;
                using var a = Client(factory, Token(rsa, subjectA));
                using var b = Client(factory, Token(rsa, subjectB));
                var originalA = await Json(a, "/api/users/me");
                var originalB = await Json(b, "/api/users/me");
                var aId = originalA.GetProperty("id").GetGuid();
                var bId = originalB.GetProperty("id").GetGuid();
                Assert.NotEqual(aId, bId);

                var spoofedId = Guid.NewGuid();
                var patch = await a.PatchAsJsonAsync("/api/users/me", new {
                    firstName = "Mary Jane", lastName = "van der Berg", city = "Auckland", bio = "F3 saved",
                    id = spoofedId, authSubjectId = subjectB, email = "spoof@example.test", reputationScore = 99
                });
                Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest,
                    (await a.PatchAsJsonAsync("/api/users/me", new { bio = new string('x', 2001) })).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest,
                    (await a.PatchAsJsonAsync("/api/users/me", new { firstName = new string('x', 201) })).StatusCode);
                var bPatch = await b.PatchAsJsonAsync("/api/users/me", new { firstName = "Account B", city = "Wellington" });
                Assert.Equal(HttpStatusCode.OK, bPatch.StatusCode);

                var aNeeds = new HashSet<Guid>();
                for (var i = 0; i < 3; i++)
                {
                    var response = await a.PostAsJsonAsync("/api/needs", new {
                        title = $"F3 A need {i} {suffix}", description = "SQL acceptance", category = "Technology",
                        skills = new[] { "Programming" }, location = "Auckland", currency = "NZD", pricingBasis = "free"
                    });
                    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                    aNeeds.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
                }
                var aOffering = await a.PostAsJsonAsync("/api/offerings", new {
                    title = $"F3 A offer {suffix}", description = "SQL acceptance", category = "Technology",
                    skills = new[] { "Programming" }, location = "Auckland", remoteAvailable = false, experienceYears = 0,
                    currency = "NZD", pricingBasis = "free"
                });
                Assert.Equal(HttpStatusCode.Created, aOffering.StatusCode);
                var aOfferingId = (await aOffering.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
                var bNeed = await b.PostAsJsonAsync("/api/needs", new {
                    title = $"F3 B need {suffix}", description = "SQL acceptance", category = "Technology",
                    skills = new[] { "Programming" }, location = "Wellington", currency = "NZD", pricingBasis = "free"
                });
                Assert.Equal(HttpStatusCode.Created, bNeed.StatusCode);
                var bNeedId = (await bNeed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

                // The same disposable rows exercise S3 SQL query translation and visibility.
                var bSearch = await Json(b, $"/api/discover/offerings?query={suffix}");
                Assert.Equal(1, bSearch.GetProperty("total").GetInt32());
                Assert.Equal(aOfferingId, bSearch.GetProperty("offerings")[0].GetProperty("id").GetGuid());
                Assert.Equal(0, (await Json(a, $"/api/discover/offerings?query={suffix}"))
                    .GetProperty("total").GetInt32());
                Assert.Equal(HttpStatusCode.OK,
                    (await b.GetAsync($"/api/discover/offerings/{aOfferingId}")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound,
                    (await a.GetAsync($"/api/discover/offerings/{aOfferingId}")).StatusCode);
                // Known nonremote Auckland/Wellington incompatibility is a hard exclusion.
                Assert.Equal(0, (await Json(b, $"/api/needs/{bNeedId}/matches")).GetArrayLength());

                var first = await Json(a, "/api/users/me/activity?page=1&pageSize=2");
                var second = await Json(a, "/api/users/me/activity?page=2&pageSize=2");
                Assert.Equal(3, first.GetProperty("needTotal").GetInt32());
                Assert.Equal(1, first.GetProperty("offeringTotal").GetInt32());
                Assert.Equal(2, first.GetProperty("needs").GetArrayLength());
                Assert.Equal(1, second.GetProperty("needs").GetArrayLength());
                Assert.True(aNeeds.SetEquals(first.GetProperty("needs").EnumerateArray()
                    .Concat(second.GetProperty("needs").EnumerateArray())
                    .Select(item => item.GetProperty("id").GetGuid())));
                var bActivity = await Json(b, "/api/users/me/activity?pageSize=2");
                Assert.Equal(1, bActivity.GetProperty("needTotal").GetInt32());
                Assert.Equal(bNeedId, bActivity.GetProperty("needs")[0].GetProperty("id").GetGuid());
                Assert.Equal(0, bActivity.GetProperty("offeringTotal").GetInt32());

                var editableNeed = first.GetProperty("needs")[0];
                var editableNeedId = editableNeed.GetProperty("id").GetGuid();
                var revision = editableNeed.GetProperty("revision").GetInt32();
                var update = new {
                    revision, title = $"F3 edited need {suffix}", description = "Edited SQL acceptance",
                    category = "Technology", skills = new[] { "Programming" }, budgetMin = (decimal?)null,
                    budgetMax = (decimal?)null, currency = "NZD", pricingBasis = "free", estimatedHours = (decimal?)null,
                    location = "Auckland", deadline = (DateTime?)null
                };
                var edit = await a.PatchAsJsonAsync($"/api/needs/{editableNeedId}", update);
                Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
                var edited = await edit.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal(revision + 1, edited.GetProperty("revision").GetInt32());
                Assert.Equal(HttpStatusCode.Conflict, (await a.PatchAsJsonAsync($"/api/needs/{editableNeedId}", update)).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await b.PatchAsJsonAsync($"/api/needs/{editableNeedId}", update)).StatusCode);
                var paused = await a.PostAsJsonAsync($"/api/needs/{editableNeedId}/pause", new { revision = revision + 1 });
                Assert.Equal(HttpStatusCode.OK, paused.StatusCode);
                Assert.Equal(HttpStatusCode.Conflict,
                    (await a.PostAsJsonAsync($"/api/needs/{editableNeedId}/pause", new { revision = revision + 1 })).StatusCode);
                var closed = await a.PostAsJsonAsync($"/api/needs/{editableNeedId}/close", new { revision = revision + 2 });
                Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
                Assert.Equal(HttpStatusCode.Conflict,
                    (await a.PostAsJsonAsync($"/api/needs/{editableNeedId}/pause", new { revision = revision + 3 })).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await b.GetAsync($"/api/needs/{aNeeds.First()}/matches")).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await b.GetAsync($"/api/offerings/{aOfferingId}/matches")).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await a.GetAsync("/api/users/me/activity?pageSize=101")).StatusCode);

                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
                var storedA = await db.Users.SingleAsync(user => user.AuthSubjectId == subjectA);
                Assert.Equal(aId, storedA.Id);
                Assert.Equal("", storedA.Email);
                Assert.Equal(0, storedA.ReputationScore);
                Assert.Equal(3, await db.Needs.CountAsync(need => need.UserId == aId));
                Assert.Equal(1, await db.Offerings.CountAsync(offering => offering.UserId == aId));
            }

            // A new host/client starts with no session and reads the same SQL rows.
            using var freshFactory = new SqlFactory(connection, rsa);
            using var anonymous = freshFactory.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users/me")).StatusCode);
            using var freshA = Client(freshFactory, Token(rsa, subjectA));
            var saved = await Json(freshA, "/api/users/me");
            Assert.Equal("Mary Jane", saved.GetProperty("firstName").GetString());
            Assert.Equal("van der Berg", saved.GetProperty("lastName").GetString());
            Assert.Equal("Auckland", saved.GetProperty("city").GetString());
            Assert.Equal("F3 saved", saved.GetProperty("bio").GetString());
            Assert.False(saved.TryGetProperty("authSubjectId", out _));
            Assert.Equal(3, (await Json(freshA, "/api/users/me/activity?pageSize=2")).GetProperty("needTotal").GetInt32());
            using var freshB = Client(freshFactory, Token(rsa, subjectB));
            Assert.Equal("Account B", (await Json(freshB, "/api/users/me")).GetProperty("firstName").GetString());
        }
        finally
        {
            if (schemaReady)
            {
                using var cleanup = new SqlFactory(connection!, rsa);
                using var scope = cleanup.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
                var users = await db.Users.Where(user => user.AuthSubjectId == subjectA || user.AuthSubjectId == subjectB).ToListAsync();
                var ids = users.Select(user => user.Id).ToArray();
                db.Needs.RemoveRange(await db.Needs.Where(need => ids.Contains(need.UserId)).ToListAsync());
                db.Offerings.RemoveRange(await db.Offerings.Where(offering => ids.Contains(offering.UserId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Users.RemoveRange(users);
                await db.SaveChangesAsync();
            }
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONNECTO_F3_SQL_CONNECTION")) ||
                Environment.GetEnvironmentVariable("CONNECTO_F3_ALLOW_DATABASE_WRITES") != "YES")
                Skip = "Requires CONNECTO_F3_SQL_CONNECTION and CONNECTO_F3_ALLOW_DATABASE_WRITES=YES.";
        }
    }

    private static async Task RequireCurrentSchema(SqlFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
        Assert.True(await db.Database.CanConnectAsync(), "SQL Server is unreachable.");
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    private static HttpClient Client(SqlFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<JsonElement> Json(HttpClient client, string path) =>
        await client.GetFromJsonAsync<JsonElement>(path);

    private static string Token(RSA rsa, string subject)
    {
        var key = new RsaSecurityKey(rsa) { KeyId = "f3-test-key" };
        var jwt = new JwtSecurityToken("https://f3-test-issuer.example", claims: [
            new Claim("sub", subject), new Claim("token_use", "access"), new Claim("client_id", "f3-test-client")],
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(20),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private sealed class SqlFactory(string connection, RSA rsa) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Cognito:Issuer", "https://f3-test-issuer.example");
            builder.UseSetting("Cognito:Audience", "f3-test-client");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ConnectoDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ConnectoDbContext>>();
                services.AddDbContext<ConnectoDbContext>(options => options.UseSqlServer(connection));
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var config = new OpenIdConnectConfiguration { Issuer = "https://f3-test-issuer.example" };
                    config.SigningKeys.Add(new RsaSecurityKey(rsa) { KeyId = "f3-test-key" });
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(config);
                });
            });
        }
    }
}
