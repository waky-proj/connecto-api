using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Connecto.Infrastructure.Services.Data;
using Connecto.Api.Media;
using SkiaSharp;
using System.Collections.Concurrent;
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
    public async Task Media_grants_attachment_hidden_serving_and_withdrawal_persist_in_sql()
    {
        var connection = Environment.GetEnvironmentVariable("CONNECTO_F3_SQL_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        Assert.Equal("YES", Environment.GetEnvironmentVariable("CONNECTO_F3_ALLOW_DATABASE_WRITES"));
        var suffix = Guid.NewGuid().ToString("N");
        var requesterSubject = $"s10-requester-{suffix}";
        var providerSubject = $"s10-provider-{suffix}";
        using var rsa = RSA.Create(2048);
        var media = new SqlMediaStore();
        Guid agreementId = Guid.Empty;
        try {
            using var factory = new SqlFactory(connection!, rsa, media);
            await RequireCurrentSchema(factory);
            using var requester = Client(factory, Token(rsa, requesterSubject));
            using var provider = Client(factory, Token(rsa, providerSubject));
            var need = await requester.PostAsJsonAsync("/api/needs", new {
                title = $"S10 need {suffix}", description = "Media SQL acceptance", category = "Technology",
                skills = new[] { "Programming" }, location = "Auckland", currency = "NZD", pricingBasis = "free"
            });
            var offer = await provider.PostAsJsonAsync("/api/offerings", new {
                title = $"S10 offer {suffix}", description = "Media SQL acceptance", category = "Technology",
                skills = new[] { "Programming" }, location = "Auckland", currency = "NZD", pricingBasis = "free", experienceYears = 0
            });
            Assert.Equal(HttpStatusCode.Created, need.StatusCode);
            Assert.Equal(HttpStatusCode.Created, offer.StatusCode);
            var needId = (await need.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var offerId = (await offer.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var proposed = await requester.PostAsJsonAsync("/api/connection-requests", new {
                needId, offeringId = offerId, needRevision = 1, offeringRevision = 1,
                terms = "S10 work terms", pricingBasis = "free"
            });
            Assert.Equal(HttpStatusCode.Created, proposed.StatusCode);
            var requestId = (await proposed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var accepted = await provider.PostAsJsonAsync($"/api/connection-requests/{requestId}/accept",
                new { revision = 1, idempotencyKey = "s10-accept" });
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            agreementId = (await accepted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var outcomePath = $"/api/agreements/{agreementId}/outcome/report";
            Assert.Equal(HttpStatusCode.OK, (await requester.PostAsJsonAsync(outcomePath,
                new { revision = 1, idempotencyKey = "s10-requester-complete", outcome = "completed" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await provider.PostAsJsonAsync(outcomePath,
                new { revision = 2, idempotencyKey = "s10-provider-complete", outcome = "completed" })).StatusCode);

            var grants = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => requester.PostAsJsonAsync("/api/media/upload-grants",
                new { agreementId, purpose = "review", kind = "outcome", contentType = "image/jpeg" })));
            Assert.Equal(3, grants.Count(x => x.StatusCode == HttpStatusCode.Created));
            Assert.Equal(5, grants.Count(x => x.StatusCode == HttpStatusCode.Conflict));
            var assetId = (await grants.First(x => x.StatusCode == HttpStatusCode.Created).Content
                .ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            using var bitmap = new SKBitmap(8, 8);
            bitmap.Erase(SKColors.Green);
            using var picture = SKImage.FromBitmap(bitmap);
            using var jpeg = picture.Encode(SKEncodedImageFormat.Jpeg, 80);
            media.Put($"quarantine/{assetId:N}", jpeg.ToArray());
            Assert.Equal(HttpStatusCode.OK, (await requester.PostAsync($"/api/media/{assetId}/finalise", null)).StatusCode);
            var reviewPath = $"/api/agreements/{agreementId}/reviews";
            Assert.Equal(HttpStatusCode.Created, (await requester.PostAsJsonAsync(reviewPath, new {
                reliability = 5, publicationDelayDays = 30, assetIds = new[] { assetId }
            })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await provider.GetAsync($"/api/media/{assetId}/content")).StatusCode);
            using var freshFactory = new SqlFactory(connection!, rsa, media);
            using var freshProvider = Client(freshFactory, Token(rsa, providerSubject));
            Assert.Equal(HttpStatusCode.Created, (await freshProvider.PostAsJsonAsync(reviewPath,
                new { communication = 4, clarity = 5, publicationDelayDays = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await freshProvider.GetAsync($"/api/media/{assetId}/content")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await requester.PostAsync($"{reviewPath}/mine/withdraw", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await freshProvider.GetAsync($"/api/media/{assetId}/content")).StatusCode);
        }
        finally {
            if (agreementId != Guid.Empty) {
                using var cleanup = new SqlFactory(connection!, rsa, media);
                using var scope = cleanup.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
                var subjects = new[] { requesterSubject, providerSubject };
                var users = await db.Users.Where(x => subjects.Contains(x.AuthSubjectId)).ToListAsync();
                var ids = users.Select(x => x.Id).ToArray();
                var assets = await db.MediaAssets.Where(x => x.EngagementId == agreementId).ToListAsync();
                var assetIds = assets.Select(x => x.Id).ToArray();
                db.MediaAccessAudits.RemoveRange(await db.MediaAccessAudits.Where(x => assetIds.Contains(x.MediaAssetId)).ToListAsync());
                db.SafetyReports.RemoveRange(await db.SafetyReports.Where(x => x.MediaAssetId != null &&
                    assetIds.Contains(x.MediaAssetId.Value)).ToListAsync());
                db.MediaAssets.RemoveRange(assets);
                db.EngagementReviews.RemoveRange(await db.EngagementReviews.Where(x => x.EngagementId == agreementId).ToListAsync());
                db.EngagementOutcomeEvents.RemoveRange(await db.EngagementOutcomeEvents.Where(x => x.EngagementId == agreementId).ToListAsync());
                await db.SaveChangesAsync();
                db.Engagements.RemoveRange(await db.Engagements.Where(x => x.Id == agreementId).ToListAsync());
                var needs = await db.Needs.Where(x => ids.Contains(x.UserId)).ToListAsync();
                var needIds = needs.Select(x => x.Id).ToArray();
                db.ConnectionRequests.RemoveRange(await db.ConnectionRequests.Where(x => needIds.Contains(x.NeedId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Needs.RemoveRange(needs);
                db.Offerings.RemoveRange(await db.Offerings.Where(x => ids.Contains(x.UserId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Users.RemoveRange(users);
                await db.SaveChangesAsync();
            }
        }
    }

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

    [SqlServerFact]
    [Trait("Category", "SqlServerAcceptance")]
    public async Task Concurrent_accepts_create_one_durable_agreement_and_retries_are_stable()
    {
        var connection = Environment.GetEnvironmentVariable("CONNECTO_F3_SQL_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        Assert.Equal("YES", Environment.GetEnvironmentVariable("CONNECTO_F3_ALLOW_DATABASE_WRITES"));
        var suffix = Guid.NewGuid().ToString("N");
        var requesterSubject = $"s6-requester-{suffix}";
        var providerASubject = $"s6-provider-a-{suffix}";
        var providerBSubject = $"s6-provider-b-{suffix}";
        using var rsa = RSA.Create(2048);
        var schemaReady = false;
        try
        {
            using var factory = new SqlFactory(connection!, rsa);
            await RequireCurrentSchema(factory);
            schemaReady = true;
            using var requester = Client(factory, Token(rsa, requesterSubject));
            using var providerA = Client(factory, Token(rsa, providerASubject));
            using var providerB = Client(factory, Token(rsa, providerBSubject));
            var requesterId = (await Json(requester, "/api/users/me")).GetProperty("id").GetGuid();
            var providerAId = (await Json(providerA, "/api/users/me")).GetProperty("id").GetGuid();
            var providerBId = (await Json(providerB, "/api/users/me")).GetProperty("id").GetGuid();
            var needResponse = await requester.PostAsJsonAsync("/api/needs", new {
                title = $"S6 need {suffix}", description = "SQL concurrent acceptance", category = "Technology",
                skills = new[] { "Programming" }, location = "Auckland", currency = "NZD", pricingBasis = "free"
            });
            Assert.Equal(HttpStatusCode.Created, needResponse.StatusCode);
            var needId = (await needResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            async Task<Guid> Propose(HttpClient provider, string label)
            {
                var offerResponse = await provider.PostAsJsonAsync("/api/offerings", new {
                    title = $"S6 {label} {suffix}", description = "SQL concurrent acceptance", category = "Technology",
                    skills = new[] { "Programming" }, location = "Auckland", remoteAvailable = false,
                    experienceYears = 0, currency = "NZD", pricingBasis = "free"
                });
                Assert.Equal(HttpStatusCode.Created, offerResponse.StatusCode);
                var offeringId = (await offerResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
                var proposed = await requester.PostAsJsonAsync("/api/connection-requests", new {
                    needId, offeringId, needRevision = 1, offeringRevision = 1,
                    terms = $"S6 agreed work {suffix}", pricingBasis = "free"
                });
                Assert.Equal(HttpStatusCode.Created, proposed.StatusCode);
                return (await proposed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            }
            var first = await Propose(providerA, "offer A");
            var second = await Propose(providerB, "offer B");
            var attempts = await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
                (i % 2 == 0 ? providerA : providerB).PostAsJsonAsync(
                    $"/api/connection-requests/{(i % 2 == 0 ? first : second)}/accept",
                    new { revision = 1, idempotencyKey = i % 2 == 0 ? "s6-key-a" : "s6-key-b" })));
            var unexpected = attempts.FirstOrDefault(response => response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Conflict));
            if (unexpected is not null)
            {
                var body = await unexpected.Content.ReadAsStringAsync();
                Assert.Fail($"Unexpected {unexpected.StatusCode}: {body[..Math.Min(3000, body.Length)]}");
            }
            Assert.All(attempts, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
            Assert.Contains(attempts, response => response.StatusCode == HttpStatusCode.OK);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
                var agreements = await db.Engagements.Where(x => x.NeedId == needId).ToListAsync();
                var agreement = Assert.Single(agreements);
                Assert.Contains(agreement.ConnectionRequestId, new[] { first, second });
                Assert.Equal(requesterId, agreement.RequesterId);
                Assert.Contains(agreement.ProviderId, new[] { providerAId, providerBId });
                var winner = agreement.ConnectionRequestId == first ? providerA : providerB;
                var loser = agreement.ConnectionRequestId == first ? providerB : providerA;
                var winnerKey = agreement.ConnectionRequestId == first ? "s6-key-a" : "s6-key-b";
                var loserId = agreement.ConnectionRequestId == first ? second : first;
                var retry = await winner.PostAsJsonAsync($"/api/connection-requests/{agreement.ConnectionRequestId}/accept",
                    new { revision = 1, idempotencyKey = winnerKey });
                Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
                Assert.Equal(agreement.Id, (await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
                Assert.Equal(HttpStatusCode.Conflict, (await loser.PostAsJsonAsync(
                    $"/api/connection-requests/{loserId}/accept",
                    new { revision = 1, idempotencyKey = "loser-retry" })).StatusCode);
                Assert.Equal("accepted", (await db.ConnectionRequests.SingleAsync(x => x.Id == agreement.ConnectionRequestId)).Status);
                Assert.Equal("pending", (await db.ConnectionRequests.SingleAsync(x => x.Id == loserId)).Status);
            }
            using var freshFactory = new SqlFactory(connection!, rsa);
            using var freshRequester = Client(freshFactory, Token(rsa, requesterSubject));
            var saved = await Json(freshRequester, "/api/agreements");
            Assert.Single(saved.EnumerateArray());
            Assert.Equal(needId, saved[0].GetProperty("needId").GetGuid());
        }
        finally
        {
            if (schemaReady)
            {
                using var cleanup = new SqlFactory(connection!, rsa);
                using var scope = cleanup.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
                var subjects = new[] { requesterSubject, providerASubject, providerBSubject };
                var users = await db.Users.Where(x => subjects.Contains(x.AuthSubjectId)).ToListAsync();
                var userIds = users.Select(x => x.Id).ToArray();
                var needs = await db.Needs.Where(x => userIds.Contains(x.UserId)).ToListAsync();
                var needIds = needs.Select(x => x.Id).ToArray();
                db.Engagements.RemoveRange(await db.Engagements.Where(x => needIds.Contains(x.NeedId)).ToListAsync());
                db.ConnectionRequests.RemoveRange(await db.ConnectionRequests.Where(x => needIds.Contains(x.NeedId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Needs.RemoveRange(needs);
                db.Offerings.RemoveRange(await db.Offerings.Where(x => userIds.Contains(x.UserId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Users.RemoveRange(users);
                await db.SaveChangesAsync();
            }
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerAcceptance")]
    public async Task Conversation_retries_receipts_and_history_survive_parallel_requests_and_restart()
    {
        var connection = Environment.GetEnvironmentVariable("CONNECTO_F3_SQL_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        Assert.Equal("YES", Environment.GetEnvironmentVariable("CONNECTO_F3_ALLOW_DATABASE_WRITES"));
        var suffix = Guid.NewGuid().ToString("N");
        var requesterSubject = $"s7-requester-{suffix}";
        var providerSubject = $"s7-provider-{suffix}";
        var outsiderSubject = $"s7-outsider-{suffix}";
        using var rsa = RSA.Create(2048);
        var schemaReady = false;
        try
        {
            using var factory = new SqlFactory(connection!, rsa);
            await RequireCurrentSchema(factory);
            schemaReady = true;
            using var requester = Client(factory, Token(rsa, requesterSubject));
            using var provider = Client(factory, Token(rsa, providerSubject));
            using var outsider = Client(factory, Token(rsa, outsiderSubject));
            var needResponse = await requester.PostAsJsonAsync("/api/needs", new {
                title = $"S7 need {suffix}", description = "Conversation SQL acceptance", category = "Technology",
                skills = new[] { "Programming" }, location = "Auckland", currency = "NZD", pricingBasis = "free"
            });
            Assert.Equal(HttpStatusCode.Created, needResponse.StatusCode);
            var needId = (await needResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var offerResponse = await provider.PostAsJsonAsync("/api/offerings", new {
                title = $"S7 offer {suffix}", description = "Conversation SQL acceptance", category = "Technology",
                skills = new[] { "Programming" }, location = "Auckland", remoteAvailable = false,
                experienceYears = 0, currency = "NZD", pricingBasis = "free"
            });
            Assert.Equal(HttpStatusCode.Created, offerResponse.StatusCode);
            var offeringId = (await offerResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var proposed = await requester.PostAsJsonAsync("/api/connection-requests", new {
                needId, offeringId, needRevision = 1, offeringRevision = 1,
                terms = $"S7 agreed work {suffix}", pricingBasis = "free"
            });
            Assert.Equal(HttpStatusCode.Created, proposed.StatusCode);
            var requestId = (await proposed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var accepted = await provider.PostAsJsonAsync($"/api/connection-requests/{requestId}/accept",
                new { revision = 1, idempotencyKey = "s7-accept" });
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var agreementId = (await accepted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var path = $"/api/agreements/{agreementId}/conversation";
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"{path}/messages")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsJsonAsync($"{path}/messages",
                new { text = "Outsider", idempotencyKey = "outsider" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsJsonAsync($"{path}/receipt",
                new { deliveredThrough = 0, readThrough = 0 })).StatusCode);

            var attempts = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
                requester.PostAsJsonAsync($"{path}/messages", new { text = "One message", idempotencyKey = "parallel-key" })));
            Assert.All(attempts, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK }));
            var ids = await Task.WhenAll(attempts.Select(async response =>
                (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64()));
            var firstId = Assert.Single(ids.Distinct());
            Assert.Equal(HttpStatusCode.Conflict, (await requester.PostAsJsonAsync($"{path}/messages",
                new { text = "Changed message", idempotencyKey = "parallel-key" })).StatusCode);
            var second = await provider.PostAsJsonAsync($"{path}/messages",
                new { text = "Reply", idempotencyKey = "reply-key" });
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
            var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
            Assert.True(secondId > firstId);
            var receipts = await Task.WhenAll(Enumerable.Range(0, 12).Select(i =>
                provider.PostAsJsonAsync($"{path}/receipt", new {
                    deliveredThrough = i % 2 == 0 ? secondId : firstId,
                    readThrough = i % 2 == 0 ? firstId : 0L
                })));
            Assert.All(receipts, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

            using var freshFactory = new SqlFactory(connection!, rsa);
            using var freshRequester = Client(freshFactory, Token(rsa, requesterSubject));
            var latest = await Json(freshRequester, $"{path}/messages?pageSize=1");
            Assert.True(latest.GetProperty("hasMore").GetBoolean());
            Assert.Equal(secondId, latest.GetProperty("messages")[0].GetProperty("id").GetInt64());
            Assert.Equal(secondId, latest.GetProperty("provider").GetProperty("deliveredThrough").GetInt64());
            Assert.Equal(firstId, latest.GetProperty("provider").GetProperty("readThrough").GetInt64());
            Assert.Equal(firstId, (await Json(freshRequester, $"{path}/messages?before={secondId}"))
                .GetProperty("messages")[0].GetProperty("id").GetInt64());
            Assert.Equal(secondId, (await Json(freshRequester, $"{path}/messages?after={firstId}"))
                .GetProperty("messages")[0].GetProperty("id").GetInt64());
            Assert.Equal($"S7 agreed work {suffix}", (await Json(freshRequester, $"/api/agreements/{agreementId}"))
                .GetProperty("terms").GetString());
            using var scope = freshFactory.Services.CreateScope();
            Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<ConnectoDbContext>()
                .ConversationMessages.CountAsync(x => x.EngagementId == agreementId));
        }
        finally
        {
            if (schemaReady)
            {
                using var cleanup = new SqlFactory(connection!, rsa);
                using var scope = cleanup.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
                var subjects = new[] { requesterSubject, providerSubject, outsiderSubject };
                var users = await db.Users.Where(x => subjects.Contains(x.AuthSubjectId)).ToListAsync();
                var userIds = users.Select(x => x.Id).ToArray();
                var needs = await db.Needs.Where(x => userIds.Contains(x.UserId)).ToListAsync();
                var needIds = needs.Select(x => x.Id).ToArray();
                var agreementIds = await db.Engagements.Where(x => needIds.Contains(x.NeedId)).Select(x => x.Id).ToArrayAsync();
                db.ConversationReceipts.RemoveRange(await db.ConversationReceipts.Where(x => agreementIds.Contains(x.EngagementId)).ToListAsync());
                db.ConversationMessages.RemoveRange(await db.ConversationMessages.Where(x => agreementIds.Contains(x.EngagementId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Engagements.RemoveRange(await db.Engagements.Where(x => agreementIds.Contains(x.Id)).ToListAsync());
                db.ConnectionRequests.RemoveRange(await db.ConnectionRequests.Where(x => needIds.Contains(x.NeedId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Needs.RemoveRange(needs);
                db.Offerings.RemoveRange(await db.Offerings.Where(x => userIds.Contains(x.UserId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Users.RemoveRange(users);
                await db.SaveChangesAsync();
            }
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerAcceptance")]
    public async Task Outcome_races_permissions_and_history_use_sql_server()
    {
        var connection = Environment.GetEnvironmentVariable("CONNECTO_F3_SQL_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var suffix = Guid.NewGuid().ToString("N");
        var requesterSubject = $"s8-requester-{suffix}";
        var providerSubject = $"s8-provider-{suffix}";
        var outsiderSubject = $"s8-outsider-{suffix}";
        using var rsa = RSA.Create(2048);
        var schemaReady = false;
        try
        {
            using var factory = new SqlFactory(connection!, rsa);
            await RequireCurrentSchema(factory);
            schemaReady = true;
            using var requester = Client(factory, Token(rsa, requesterSubject));
            using var provider = Client(factory, Token(rsa, providerSubject));
            using var outsider = Client(factory, Token(rsa, outsiderSubject));
            var need = await requester.PostAsJsonAsync("/api/needs", new {
                title = $"S8 need {suffix}", description = "Outcome race acceptance", category = "Technology",
                skills = new[] { "Programming" }, location = "Auckland", currency = "NZD", pricingBasis = "free"
            });
            var offer = await provider.PostAsJsonAsync("/api/offerings", new {
                title = $"S8 offer {suffix}", description = "Outcome race acceptance", category = "Technology",
                skills = new[] { "Programming" }, location = "Auckland", remoteAvailable = false,
                experienceYears = 0, currency = "NZD", pricingBasis = "free"
            });
            Assert.Equal(HttpStatusCode.Created, need.StatusCode);
            Assert.Equal(HttpStatusCode.Created, offer.StatusCode);
            var needId = (await need.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var offerId = (await offer.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var proposed = await requester.PostAsJsonAsync("/api/connection-requests", new {
                needId, offeringId = offerId, needRevision = 1, offeringRevision = 1,
                terms = $"S8 agreed work {suffix}", pricingBasis = "free"
            });
            Assert.Equal(HttpStatusCode.Created, proposed.StatusCode);
            var requestId = (await proposed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var accepted = await provider.PostAsJsonAsync($"/api/connection-requests/{requestId}/accept",
                new { revision = 1, idempotencyKey = "s8-accept" });
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var id = (await accepted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var path = $"/api/agreements/{id}/outcome";
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsJsonAsync($"{path}/cancel",
                new { revision = 1, idempotencyKey = "outsider", reason = "Outsider" })).StatusCode);
            var attempts = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
                requester.PostAsJsonAsync($"{path}/report", new {
                    revision = 1, idempotencyKey = "same-report", outcome = "completed"
                })));
            Assert.All(attempts, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Assert.Equal(HttpStatusCode.Conflict, (await provider.PostAsJsonAsync($"{path}/report",
                new { revision = 1, idempotencyKey = "stale-report", outcome = "completed" })).StatusCode);
            var competing = await Task.WhenAll(
                provider.PostAsJsonAsync($"{path}/report", new {
                    revision = 2, idempotencyKey = "provider-report", outcome = "completed"
                }),
                requester.PostAsJsonAsync($"{path}/cancel", new {
                    revision = 2, idempotencyKey = "requester-cancel", reason = "Changed plans"
                }));
            Assert.Single(competing, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(competing, x => x.StatusCode == HttpStatusCode.Conflict);
            var secondNeed = await requester.PostAsJsonAsync("/api/needs", new {
                title = $"S8 disputed need {suffix}", description = "Conflicting reports", category = "Technology",
                skills = new[] { "Programming" }, location = "Auckland", currency = "NZD", pricingBasis = "free"
            });
            Assert.Equal(HttpStatusCode.Created, secondNeed.StatusCode);
            var secondNeedId = (await secondNeed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var secondRequest = await requester.PostAsJsonAsync("/api/connection-requests", new {
                needId = secondNeedId, offeringId = offerId, needRevision = 1, offeringRevision = 1,
                terms = $"S8 disputed work {suffix}", pricingBasis = "free"
            });
            Assert.Equal(HttpStatusCode.Created, secondRequest.StatusCode);
            var secondRequestId = (await secondRequest.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var secondAgreement = await provider.PostAsJsonAsync($"/api/connection-requests/{secondRequestId}/accept",
                new { revision = 1, idempotencyKey = "s8-accept-disputed" });
            Assert.Equal(HttpStatusCode.OK, secondAgreement.StatusCode);
            var secondId = (await secondAgreement.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var secondPath = $"/api/agreements/{secondId}/outcome";
            Assert.Equal(HttpStatusCode.OK, (await requester.PostAsJsonAsync($"{secondPath}/report",
                new { revision = 1, idempotencyKey = "incomplete", outcome = "incomplete" })).StatusCode);
            var disagreement = await provider.PostAsJsonAsync($"{secondPath}/report",
                new { revision = 2, idempotencyKey = "completed", outcome = "completed" });
            Assert.Equal(HttpStatusCode.OK, disagreement.StatusCode);
            Assert.Equal("disputed", (await disagreement.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("outcomeState").GetString());
            using var freshFactory = new SqlFactory(connection!, rsa);
            using var freshRequester = Client(freshFactory, Token(rsa, requesterSubject));
            var current = await Json(freshRequester, path);
            Assert.Equal(2, current.GetProperty("history").GetArrayLength());
            Assert.Contains(current.GetProperty("outcomeState").GetString(), new[] { "both_confirmed", "cancelled" });
            using var scope = freshFactory.Services.CreateScope();
            Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<ConnectoDbContext>()
                .EngagementOutcomeEvents.CountAsync(x => x.EngagementId == id));
        }
        finally
        {
            if (schemaReady)
            {
                using var cleanup = new SqlFactory(connection!, rsa);
                using var scope = cleanup.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
                var subjects = new[] { requesterSubject, providerSubject, outsiderSubject };
                var users = await db.Users.Where(x => subjects.Contains(x.AuthSubjectId)).ToListAsync();
                var userIds = users.Select(x => x.Id).ToArray();
                var needs = await db.Needs.Where(x => userIds.Contains(x.UserId)).ToListAsync();
                var needIds = needs.Select(x => x.Id).ToArray();
                var agreements = await db.Engagements.Where(x => needIds.Contains(x.NeedId)).ToListAsync();
                var agreementIds = agreements.Select(x => x.Id).ToArray();
                db.EngagementOutcomeEvents.RemoveRange(await db.EngagementOutcomeEvents.Where(x => agreementIds.Contains(x.EngagementId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Engagements.RemoveRange(agreements);
                db.ConnectionRequests.RemoveRange(await db.ConnectionRequests.Where(x => needIds.Contains(x.NeedId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Needs.RemoveRange(needs);
                db.Offerings.RemoveRange(await db.Offerings.Where(x => userIds.Contains(x.UserId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Users.RemoveRange(users);
                await db.SaveChangesAsync();
            }
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerAcceptance")]
    public async Task Review_submissions_are_unique_hidden_and_persist_across_hosts()
    {
        var connection = Environment.GetEnvironmentVariable("CONNECTO_F3_SQL_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        Assert.Equal("YES", Environment.GetEnvironmentVariable("CONNECTO_F3_ALLOW_DATABASE_WRITES"));
        var suffix = Guid.NewGuid().ToString("N");
        var requesterSubject = $"s9-requester-{suffix}";
        var providerSubject = $"s9-provider-{suffix}";
        using var rsa = RSA.Create(2048);
        var schemaReady = false;
        try
        {
            using var factory = new SqlFactory(connection!, rsa);
            await RequireCurrentSchema(factory);
            schemaReady = true;
            using var requester = Client(factory, Token(rsa, requesterSubject));
            using var provider = Client(factory, Token(rsa, providerSubject));
            var need = await requester.PostAsJsonAsync("/api/needs", new {
                title = $"S9 need {suffix}", description = "Review SQL acceptance", category = "Technology",
                skills = new[] { "Programming" }, location = "Auckland", currency = "NZD", pricingBasis = "free"
            });
            var offer = await provider.PostAsJsonAsync("/api/offerings", new {
                title = $"S9 offer {suffix}", description = "Review SQL acceptance", category = "Technology",
                skills = new[] { "Programming" }, location = "Auckland", currency = "NZD", pricingBasis = "free", experienceYears = 0
            });
            Assert.Equal(HttpStatusCode.Created, need.StatusCode);
            Assert.Equal(HttpStatusCode.Created, offer.StatusCode);
            var needId = (await need.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var offerId = (await offer.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var proposed = await requester.PostAsJsonAsync("/api/connection-requests", new {
                needId, offeringId = offerId, needRevision = 1, offeringRevision = 1,
                terms = "S9 work terms", pricingBasis = "free"
            });
            Assert.Equal(HttpStatusCode.Created, proposed.StatusCode);
            var requestId = (await proposed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var accepted = await provider.PostAsJsonAsync($"/api/connection-requests/{requestId}/accept",
                new { revision = 1, idempotencyKey = "s9-accept" });
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var agreementId = (await accepted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var outcomePath = $"/api/agreements/{agreementId}/outcome/report";
            Assert.Equal(HttpStatusCode.OK, (await requester.PostAsJsonAsync(outcomePath,
                new { revision = 1, idempotencyKey = "s9-requester-complete", outcome = "completed" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await provider.PostAsJsonAsync(outcomePath,
                new { revision = 2, idempotencyKey = "s9-provider-complete", outcome = "completed" })).StatusCode);
            var reviewPath = $"/api/agreements/{agreementId}/reviews";
            var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => requester.PostAsJsonAsync(reviewPath,
                new { reliability = 5, communication = 4, agreement = 5, publicationDelayDays = 30 })));
            Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.Created);
            Assert.Equal(7, attempts.Count(x => x.StatusCode == HttpStatusCode.Conflict));
            Assert.Equal(JsonValueKind.Null, (await Json(provider, reviewPath)).GetProperty("reciprocal").ValueKind);
            using var freshFactory = new SqlFactory(connection!, rsa);
            using var freshProvider = Client(freshFactory, Token(rsa, providerSubject));
            var providerReview = await freshProvider.PostAsJsonAsync(reviewPath,
                new { communication = 4, clarity = 5, commitments = 3, publicationDelayDays = 1 });
            Assert.Equal(HttpStatusCode.Created, providerReview.StatusCode);
            Assert.NotEqual(JsonValueKind.Null, (await Json(freshProvider, reviewPath)).GetProperty("reciprocal").ValueKind);
            var providerId = (await Json(freshProvider, "/api/users/me")).GetProperty("id").GetGuid();
            Assert.Equal(1, (await Json(freshProvider, $"/api/members/{providerId}/reputation"))
                .GetProperty("asProvider").GetProperty("reviewCount").GetInt32());
            Assert.Equal(HttpStatusCode.OK, (await requester.PostAsync($"{reviewPath}/mine/withdraw", null)).StatusCode);
            Assert.Equal(0, (await Json(freshProvider, $"/api/members/{providerId}/reputation"))
                .GetProperty("asProvider").GetProperty("reviewCount").GetInt32());
        }
        finally
        {
            if (schemaReady)
            {
                using var cleanup = new SqlFactory(connection!, rsa);
                using var scope = cleanup.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
                var subjects = new[] { requesterSubject, providerSubject };
                var users = await db.Users.Where(x => subjects.Contains(x.AuthSubjectId)).ToListAsync();
                var ids = users.Select(x => x.Id).ToArray();
                var needs = await db.Needs.Where(x => ids.Contains(x.UserId)).ToListAsync();
                var needIds = needs.Select(x => x.Id).ToArray();
                var agreements = await db.Engagements.Where(x => needIds.Contains(x.NeedId)).ToListAsync();
                var agreementIds = agreements.Select(x => x.Id).ToArray();
                db.EngagementReviews.RemoveRange(await db.EngagementReviews.Where(x => agreementIds.Contains(x.EngagementId)).ToListAsync());
                db.EngagementOutcomeEvents.RemoveRange(await db.EngagementOutcomeEvents.Where(x => agreementIds.Contains(x.EngagementId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Engagements.RemoveRange(agreements);
                db.ConnectionRequests.RemoveRange(await db.ConnectionRequests.Where(x => needIds.Contains(x.NeedId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Needs.RemoveRange(needs);
                db.Offerings.RemoveRange(await db.Offerings.Where(x => ids.Contains(x.UserId)).ToListAsync());
                await db.SaveChangesAsync();
                db.Users.RemoveRange(users);
                await db.SaveChangesAsync();
            }
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerAcceptance")]
    public async Task Pilot_signals_migrate_deduplicate_and_report_on_sql_server()
    {
        var connection = Environment.GetEnvironmentVariable("CONNECTO_F3_SQL_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        Assert.Equal("YES", Environment.GetEnvironmentVariable("CONNECTO_F3_ALLOW_DATABASE_WRITES"));
        var suffix = Guid.NewGuid().ToString("N");
        var requesterSubject = $"s11-requester-{suffix}";
        var providerSubject = $"s11-provider-{suffix}";
        using var rsa = RSA.Create(2048);
        Guid needId = Guid.Empty, offeringId = Guid.Empty;
        try
        {
            using var factory = new SqlFactory(connection!, rsa);
            await RequireCurrentSchema(factory);
            using var requester = Client(factory, Token(rsa, requesterSubject));
            using var provider = Client(factory, Token(rsa, providerSubject));
            var needResponse = await requester.PostAsJsonAsync("/api/needs", new {
                title = $"S11 garden need {suffix}", description = "Garden work", category = "Home and Garden",
                skills = new[] { "Gardening" }, location = "Auckland", currency = "NZD", pricingBasis = "negotiable"
            });
            var offerResponse = await provider.PostAsJsonAsync("/api/offerings", new {
                title = $"S11 garden offering {suffix}", description = "Garden work", category = "Home and Garden",
                skills = new[] { "Gardening" }, location = "Auckland", currency = "NZD", pricingBasis = "negotiable", experienceYears = 0
            });
            Assert.Equal(HttpStatusCode.Created, needResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Created, offerResponse.StatusCode);
            needId = (await needResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            offeringId = (await offerResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var discovery = new { needId, clientKey = $"discovery-{suffix}" };
            Assert.Equal(HttpStatusCode.Created,
                (await requester.PostAsJsonAsync("/api/pilot/signals/discovery", discovery)).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await requester.PostAsJsonAsync("/api/pilot/signals/discovery", discovery)).StatusCode);
            var suitable = new { needId, offeringId, clientKey = $"suitable-{suffix}" };
            Assert.Equal(HttpStatusCode.Created,
                (await requester.PostAsJsonAsync("/api/pilot/signals/suitable", suitable)).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await requester.PostAsJsonAsync("/api/pilot/signals/suitable", suitable)).StatusCode);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
                Assert.Equal(2, await db.PilotSignals.CountAsync(x => x.NeedId == needId));
            }
            using var staff = Client(factory, Token(rsa, $"s11-operator-{suffix}", "safety-operators"));
            var from = Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(-5).ToString("O"));
            var to = Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(1).ToString("O"));
            var report = await Json(staff, $"/api/pilot/metrics?from={from}&to={to}");
            Assert.True(report.GetProperty("discovery").GetProperty("searchedNeeds").GetInt32() >= 1);
            Assert.True(report.GetProperty("suitability").GetProperty("markedNeedOfferingPairs").GetInt32() >= 1);
        }
        finally
        {
            using var cleanup = new SqlFactory(connection!, rsa);
            using var scope = cleanup.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            if (needId != Guid.Empty)
            {
                db.PilotSignals.RemoveRange(await db.PilotSignals.Where(x => x.NeedId == needId).ToListAsync());
                await db.SaveChangesAsync();
                db.NeedSkills.RemoveRange(await db.NeedSkills.Where(x => x.NeedId == needId).ToListAsync());
                db.Needs.RemoveRange(await db.Needs.Where(x => x.Id == needId).ToListAsync());
            }
            if (offeringId != Guid.Empty)
            {
                db.OfferingSkills.RemoveRange(await db.OfferingSkills.Where(x => x.OfferingId == offeringId).ToListAsync());
                db.Offerings.RemoveRange(await db.Offerings.Where(x => x.Id == offeringId).ToListAsync());
            }
            await db.SaveChangesAsync();
            db.Users.RemoveRange(await db.Users.Where(x => x.AuthSubjectId == requesterSubject || x.AuthSubjectId == providerSubject).ToListAsync());
            await db.SaveChangesAsync();
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

    private static string Token(RSA rsa, string subject, string? group = null)
    {
        var key = new RsaSecurityKey(rsa) { KeyId = "f3-test-key" };
        var claims = new List<Claim> { new("sub", subject), new("token_use", "access"), new("client_id", "f3-test-client") };
        if (group is not null) claims.Add(new Claim("cognito:groups", group));
        var jwt = new JwtSecurityToken("https://f3-test-issuer.example", claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(20),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private sealed class SqlFactory(string connection, RSA rsa, SqlMediaStore? media = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Cognito:Issuer", "https://f3-test-issuer.example");
            builder.UseSetting("Cognito:Audience", "f3-test-client");
            builder.UseSetting("Safety:OperatorGroup", "safety-operators");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ConnectoDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ConnectoDbContext>>();
                services.AddDbContext<ConnectoDbContext>(options => options.UseSqlServer(connection));
                if (media is not null) { services.RemoveAll<IMediaObjectStore>(); services.AddSingleton<IMediaObjectStore>(media); }
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var config = new OpenIdConnectConfiguration { Issuer = "https://f3-test-issuer.example" };
                    config.SigningKeys.Add(new RsaSecurityKey(rsa) { KeyId = "f3-test-key" });
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(config);
                });
            });
        }
    }

    private sealed class SqlMediaStore : IMediaObjectStore
    {
        private readonly ConcurrentDictionary<string, byte[]> objects = new();
        public bool IsAvailable => true;
        public void Put(string key, byte[] bytes) => objects[key] = bytes;
        public Task<(string Url, Dictionary<string, string> Fields)> UploadGrantAsync(string key, string contentType, DateTime expiresAt) =>
            Task.FromResult(("https://private.invalid/upload", new Dictionary<string, string> { ["key"] = key }));
        public Task<(Stream Body, long Length)> ReadAsync(string key, CancellationToken ct) {
            var bytes = objects[key]; return Task.FromResult<(Stream, long)>((new MemoryStream(bytes), bytes.Length));
        }
        public async Task WriteAsync(string key, Stream body, string contentType, CancellationToken ct) {
            using var memory = new MemoryStream(); await body.CopyToAsync(memory, ct); objects[key] = memory.ToArray();
        }
        public Task DeleteAsync(string key, CancellationToken ct) { objects.TryRemove(key, out _); return Task.CompletedTask; }
    }
}
