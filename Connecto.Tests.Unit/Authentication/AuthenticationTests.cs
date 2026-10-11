using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Connecto.Api.Authentication;
using Connecto.Api.Safety;
using Connecto.Api.Media;
using SkiaSharp;
using System.Collections.Concurrent;
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
            new CreateNeedRequest("Need", "Help", "Technology", ["Programming"], null, null, null, null, "NZD", "free"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/offerings",
            new CreateOfferingRequest("Offering", "Help", "Technology", ["Programming"], null, null, "Auckland", false, null, null, 0, "NZD", "free"))).StatusCode);
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
    public async Task New_listings_require_managed_NZD_terms_and_return_their_revision()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        var invalid = await client.PostAsJsonAsync("/api/needs", new CreateNeedRequest(
            "Invalid", "Description", "Technical", ["Programming"], 1, 10, "Auckland", null,
            "USD", "fixed"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var created = await client.PostAsJsonAsync("/api/needs", new CreateNeedRequest(
            "Hourly", "Description", "Technology", ["Programming"], 25, 50, "Auckland", null,
            "NZD", "hourly", 2));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var listing = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("NZD", listing.GetProperty("currency").GetString());
        Assert.Equal(2, listing.GetProperty("pricingBasis").GetInt32());
        Assert.Equal(1, listing.GetProperty("revision").GetInt32());
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
        var response = await client.PostAsJsonAsync("/api/needs", new CreateNeedRequest("Private A", "Description", "Technology", ["Programming"], 1, 10, "Auckland", null, "NZD", "fixed"));
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
    public async Task Block_hides_discovery_and_matches_and_unblock_restores_visibility()
    {
        using var factory = new ApiFactory();
        using var a = factory.CreateClient();
        a.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        var need = await (await a.PostAsJsonAsync("/api/needs", new CreateNeedRequest(
            "Laptop help", "Description", "Technology", ["Programming"], null, null,
            "Auckland", null, "NZD", "negotiable"))).Content.ReadFromJsonAsync<Listing>();
        using var b = factory.CreateClient();
        b.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "subject-2"));
        var member = await b.GetFromJsonAsync<Profile>("/api/users/me");
        var offer = await (await b.PostAsJsonAsync("/api/offerings", new CreateOfferingRequest(
            "Laptop help", "Description", "Technology", ["Programming"], null, null,
            "Auckland", false, null, null, 0, "NZD", "negotiable"))).Content.ReadFromJsonAsync<Listing>();
        Assert.Equal(1, (await a.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/discover/offerings")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await a.PutAsync($"/api/safety/blocks/{member!.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PutAsync($"/api/safety/blocks/{member.Id}", null)).StatusCode);
        Assert.Equal(0, (await a.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/discover/offerings")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/discover/offerings/{offer!.Id}")).StatusCode);
        Assert.Equal(0, (await a.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/needs/{need!.Id}/matches")).GetArrayLength());
        Assert.Equal(0, (await b.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/discover/offerings")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/safety/blocks/{member.Id}")).StatusCode);
        Assert.Equal(1, (await a.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/discover/offerings")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Reports_are_private_operator_cases_require_group_and_outbox_replays_once()
    {
        using var factory = new ApiFactory();
        using var a = factory.CreateClient();
        a.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        using var b = factory.CreateClient();
        b.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "subject-2"));
        var subject = await b.GetFromJsonAsync<Profile>("/api/users/me");
        var created = await a.PostAsJsonAsync("/api/safety/reports", new {
            subjectId = subject!.Id, reason = "unsafe", details = "Concerning interaction" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var caseId = body.GetProperty("id").GetGuid();
        Assert.Single((await a.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/safety/reports")).EnumerateArray());
        Assert.Equal(0, (await b.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/safety/reports")).GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await b.GetAsync("/api/safety/cases")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.GetAsync($"/api/safety/cases/{caseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.PostAsync($"/api/safety/cases/{caseId}/review", null)).StatusCode);
        using var operatorClient = factory.CreateClient();
        operatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "operator", group: "safety-operators"));
        Assert.Single((await operatorClient.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/safety/cases")).EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await operatorClient.PostAsync($"/api/safety/cases/{caseId}/review", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await operatorClient.PostAsync($"/api/safety/cases/{caseId}/review", null)).StatusCode);

        var dispatcher = factory.Services.GetRequiredService<OutboxDispatcher>();
        await dispatcher.DispatchBatchAsync();
        await dispatcher.DispatchBatchAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
        Assert.Equal(2, await db.OutboxEvents.CountAsync());
        Assert.Equal(2, await db.ConsumerReceipts.CountAsync());
        Assert.Equal(2, await db.JourneyEvents.CountAsync());
        Assert.All(await db.OutboxEvents.ToListAsync(), item => Assert.NotNull(item.DeliveredAt));
        // Simulate a dispatcher crash after its consumer commit but before an ack is observed.
        foreach (var item in await db.OutboxEvents.ToListAsync()) item.DeliveredAt = null;
        await db.SaveChangesAsync();
        await dispatcher.DispatchBatchAsync();
        Assert.Equal(2, await db.ConsumerReceipts.CountAsync());
        Assert.Equal(2, await db.JourneyEvents.CountAsync());
    }

    [Fact]
    public async Task Discovery_uses_persisted_public_details_and_excludes_self_and_paused_listings()
    {
        using var factory = new ApiFactory();
        using var a = factory.CreateClient();
        a.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        var needResponse = await a.PostAsJsonAsync("/api/needs", new CreateNeedRequest(
            "Computer help", "Laptop setup", "Technology", ["Programming"], null, null,
            "Auckland", null, "NZD", "negotiable"));
        var need = await needResponse.Content.ReadFromJsonAsync<Listing>();
        var ownResponse = await a.PostAsJsonAsync("/api/offerings", new CreateOfferingRequest(
            "Own laptop help", "Private", "Technology", ["Programming"], null, null,
            "Auckland", false, null, null, 0, "NZD", "negotiable"));
        var own = await ownResponse.Content.ReadFromJsonAsync<Listing>();

        using var b = factory.CreateClient();
        b.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "subject-2"));
        await b.PatchAsJsonAsync("/api/users/me", new { firstName = "Casey", city = "Auckland" });
        var publicResponse = await b.PostAsJsonAsync("/api/offerings", new CreateOfferingRequest(
            "Laptop setup", "Computer guidance", "Technology", ["Programming"], null, null,
            "Auckland", false, null, null, 0, "NZD", "negotiable"));
        var visible = await publicResponse.Content.ReadFromJsonAsync<Listing>();

        var search = await a.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/discover/offerings?query=computer");
        Assert.Equal(1, search.GetProperty("total").GetInt32());
        var offer = search.GetProperty("offerings")[0];
        Assert.Equal(visible!.Id, offer.GetProperty("id").GetGuid());
        Assert.Equal("Casey", offer.GetProperty("providerName").GetString());
        Assert.False(offer.TryGetProperty("authSubjectId", out _));
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/discover/offerings/{own!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/discover/offerings/{visible.Id}")).StatusCode);
        var today = await a.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/discover/offerings?availableToday=true");
        Assert.Equal(0, today.GetProperty("total").GetInt32()); // An unknown start date is not confirmed availability.

        var matches = await a.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/needs/{need!.Id}/matches");
        Assert.Equal(1, matches.GetArrayLength());
        Assert.Equal(visible.Id, matches[0].GetProperty("offering").GetProperty("id").GetGuid());
        Assert.Equal("s3-v1", matches[0].GetProperty("policyVersion").GetString());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            (await db.Offerings.FindAsync(visible.Id))!.Status = OfferingStatus.Paused;
            await db.SaveChangesAsync();
        }
        var afterPause = await a.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/discover/offerings?query=computer");
        Assert.Equal(0, afterPause.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/discover/offerings/{visible.Id}")).StatusCode);
    }

    [Fact]
    public async Task Listing_creation_ignores_spoofed_user_header_and_other_users_cannot_read_matches()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());
        var needResponse = await client.PostAsJsonAsync("/api/needs", new CreateNeedRequest("Need", "Description", "Technology", ["Programming"], null, null, null, null, "NZD", "negotiable"));
        Assert.Equal(HttpStatusCode.Created, needResponse.StatusCode);
        var need = await needResponse.Content.ReadFromJsonAsync<Listing>();
        var offeringResponse = await client.PostAsJsonAsync("/api/offerings", new CreateOfferingRequest("Offering", "Description", "Technology", ["Programming"], null, null, "Auckland", false, null, null, 0, "NZD", "negotiable"));
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
    [Fact]
    public async Task Connection_requests_are_persistent_participant_only_and_have_guarded_transitions()
    {
        using var factory = new ApiFactory();
        using var sender = factory.CreateClient();
        sender.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        var needResponse = await sender.PostAsJsonAsync("/api/needs", new CreateNeedRequest(
            "Garden help", "Need some weeding", "Home and Garden", ["Gardening"], null, null,
            "Auckland", null, "NZD", "negotiable"));
        Assert.Equal(HttpStatusCode.Created, needResponse.StatusCode);
        var need = await needResponse.Content.ReadFromJsonAsync<Listing>();

        using var recipient = factory.CreateClient();
        recipient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "recipient"));
        var offeringResponse = await recipient.PostAsJsonAsync("/api/offerings", new CreateOfferingRequest(
            "Garden help", "Can help with weeding", "Home and Garden", ["Gardening"], null, null,
            "Auckland", false, null, null, 0, "NZD", "negotiable"));
        Assert.Equal(HttpStatusCode.Created, offeringResponse.StatusCode);
        var offering = await offeringResponse.Content.ReadFromJsonAsync<Listing>();
        var body = new { needId = need!.Id, offeringId = offering!.Id, needRevision = 1,
            offeringRevision = 1, terms = "Weed the front garden on Saturday morning.",
            pricingBasis = "fixed", proposedAmount = 40m };

        Assert.Equal(HttpStatusCode.Forbidden,
            (await recipient.PostAsJsonAsync("/api/connection-requests", body)).StatusCode);
        var created = await sender.PostAsJsonAsync("/api/connection-requests", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = item.GetProperty("id").GetGuid();
        Assert.Equal("pending", item.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await sender.PostAsJsonAsync("/api/connection-requests", body)).StatusCode);

        using var outsider = factory.CreateClient();
        outsider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "outsider"));
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/connection-requests/{id}")).StatusCode);
        var outsiderInbox = await outsider.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/connection-requests");
        Assert.Equal(0, outsiderInbox.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.PostAsJsonAsync($"/api/connection-requests/{id}/decline", new { revision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await sender.PostAsJsonAsync($"/api/connection-requests/{id}/decline", new { revision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await recipient.PostAsJsonAsync($"/api/connection-requests/{id}/withdraw", new { revision = 1 })).StatusCode);

        using var freshRecipient = factory.CreateClient();
        freshRecipient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "recipient"));
        var inbox = await freshRecipient.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/connection-requests");
        Assert.Equal(1, inbox.GetProperty("total").GetInt32());
        Assert.Equal(id, inbox.GetProperty("requests")[0].GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK,
            (await freshRecipient.PostAsJsonAsync($"/api/connection-requests/{id}/decline", new { revision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await sender.PostAsJsonAsync($"/api/connection-requests/{id}/withdraw", new { revision = 1 })).StatusCode);
        var persisted = await sender.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/connection-requests/{id}");
        Assert.Equal("declined", persisted.GetProperty("status").GetString());
        Assert.Equal(2, persisted.GetProperty("revision").GetInt32());
        var second = await sender.PostAsJsonAsync("/api/connection-requests", body);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondItem = await second.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var secondId = secondItem.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK,
            (await sender.PostAsJsonAsync($"/api/connection-requests/{secondId}/withdraw", new { revision = 1 })).StatusCode);
        Assert.Equal("withdrawn", (await recipient.GetFromJsonAsync<System.Text.Json.JsonElement>(
            $"/api/connection-requests/{secondId}")).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await sender.PostAsJsonAsync("/api/connection-requests", new { body.needId, body.offeringId,
                needRevision = 0, body.offeringRevision, body.terms, body.pricingBasis, body.proposedAmount })).StatusCode);
    }

    [Fact]
    public async Task Acceptance_is_participant_only_idempotent_and_limited_to_one_agreement_per_need()
    {
        using var factory = new ApiFactory();
        using var requester = factory.CreateClient();
        using var providerA = factory.CreateClient();
        using var providerB = factory.CreateClient();
        using var outsider = factory.CreateClient();
        requester.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        providerA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "provider-a"));
        providerB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "provider-b"));
        outsider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "outsider-s6"));
        var needResponse = await requester.PostAsJsonAsync("/api/needs", new CreateNeedRequest(
            "Garden help", "Need some weeding", "Home and Garden", ["Gardening"], null, null,
            "Auckland", null, "NZD", "negotiable"));
        var need = await needResponse.Content.ReadFromJsonAsync<Listing>();
        async Task<Guid> Propose(HttpClient provider, string title)
        {
            var offerResponse = await provider.PostAsJsonAsync("/api/offerings", new CreateOfferingRequest(
                title, "Can help with weeding", "Home and Garden", ["Gardening"], null, null,
                "Auckland", false, null, null, 0, "NZD", "negotiable"));
            var offer = await offerResponse.Content.ReadFromJsonAsync<Listing>();
            var created = await requester.PostAsJsonAsync("/api/connection-requests", new {
                needId = need!.Id, offeringId = offer!.Id, needRevision = 1, offeringRevision = 1,
                terms = "Weed the front garden on Saturday morning.", pricingBasis = "fixed", proposedAmount = 40m
            });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            return (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        }
        var first = await Propose(providerA, "First provider");
        var second = await Propose(providerB, "Second provider");
        var body = new { revision = 1, idempotencyKey = "s6-accept-first" };
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.PostAsJsonAsync($"/api/connection-requests/{first}/accept", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await requester.PostAsJsonAsync($"/api/connection-requests/{first}/accept", body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await providerA.PostAsJsonAsync($"/api/connection-requests/{first}/accept", new { revision = 1 })).StatusCode);
        var accepted = await providerA.PostAsJsonAsync($"/api/connection-requests/{first}/accept", body);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var agreement = await accepted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var agreementId = agreement.GetProperty("id").GetGuid();
        Assert.Equal("Weed the front garden on Saturday morning.", agreement.GetProperty("terms").GetString());
        Assert.Equal(40m, agreement.GetProperty("agreedAmount").GetDecimal());
        var retry = await providerA.PostAsJsonAsync($"/api/connection-requests/{first}/accept", body);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(agreementId, (await retry.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict,
            (await providerA.PostAsJsonAsync($"/api/connection-requests/{first}/accept", new { revision = 2, idempotencyKey = "s6-accept-first" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await providerB.PostAsJsonAsync($"/api/connection-requests/{second}/accept", new { revision = 1, idempotencyKey = "s6-accept-second" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/agreements/{agreementId}")).StatusCode);
        var participantAgreement = await requester.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/agreements/{agreementId}");
        Assert.Equal(agreementId, participantAgreement.GetProperty("id").GetGuid());
        var outsiderList = await outsider.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/agreements");
        Assert.Equal(0, outsiderList.GetArrayLength());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
        Assert.Equal(1, await db.Engagements.CountAsync());
        var changedNeed = await db.Needs.SingleAsync(x => x.Id == need!.Id);
        changedNeed.Title = "Edited after acceptance";
        changedNeed.Revision++;
        await db.SaveChangesAsync();
        var frozen = await providerA.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/agreements/{agreementId}");
        Assert.Equal("Garden help", frozen.GetProperty("needTitle").GetString());
        Assert.Equal("Weed the front garden on Saturday morning.", frozen.GetProperty("terms").GetString());
    }

    [Fact]
    public async Task Conversation_is_persistent_ordered_idempotent_and_participant_only()
    {
        using var factory = new ApiFactory();
        using var requester = factory.CreateClient();
        using var provider = factory.CreateClient();
        using var outsider = factory.CreateClient();
        requester.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        provider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "provider-s7"));
        outsider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "outsider-s7"));
        var requesterId = (await requester.GetFromJsonAsync<Profile>("/api/users/me"))!.Id;
        var need = await (await requester.PostAsJsonAsync("/api/needs", new CreateNeedRequest(
            "Garden help", "Need some weeding", "Home and Garden", ["Gardening"], null, null,
            "Auckland", null, "NZD", "negotiable"))).Content.ReadFromJsonAsync<Listing>();
        var offering = await (await provider.PostAsJsonAsync("/api/offerings", new CreateOfferingRequest(
            "Garden work", "Can help with weeding", "Home and Garden", ["Gardening"], null, null,
            "Auckland", false, null, null, 0, "NZD", "negotiable"))).Content.ReadFromJsonAsync<Listing>();
        var created = await requester.PostAsJsonAsync("/api/connection-requests", new {
            needId = need!.Id, offeringId = offering!.Id, needRevision = 1, offeringRevision = 1,
            terms = "Weed the garden on Saturday morning.", pricingBasis = "free"
        });
        var requestId = (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        var accepted = await provider.PostAsJsonAsync($"/api/connection-requests/{requestId}/accept",
            new { revision = 1, idempotencyKey = "s7-agreement" });
        var agreement = await accepted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = agreement.GetProperty("id").GetGuid();
        var path = $"/api/agreements/{id}/conversation";
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"{path}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsJsonAsync($"{path}/messages",
            new { text = "Intrusion", idempotencyKey = "x" })).StatusCode);
        var first = await requester.PostAsJsonAsync($"{path}/messages", new { text = "Saturday works.", idempotencyKey = "msg-1" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstMessage = await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var firstId = firstMessage.GetProperty("id").GetInt64();
        var retry = await requester.PostAsJsonAsync($"{path}/messages", new { text = "Saturday works.", idempotencyKey = "msg-1" });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(firstId, (await retry.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict, (await requester.PostAsJsonAsync($"{path}/messages",
            new { text = "Different text", idempotencyKey = "msg-1" })).StatusCode);
        var second = await provider.PostAsJsonAsync($"{path}/messages", new { text = "See you then.", idempotencyKey = "msg-2" });
        var secondId = (await second.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetInt64();
        Assert.True(secondId > firstId);
        using var freshRequester = factory.CreateClient();
        freshRequester.DefaultRequestHeaders.Authorization = requester.DefaultRequestHeaders.Authorization;
        var history = await freshRequester.GetFromJsonAsync<System.Text.Json.JsonElement>($"{path}/messages?pageSize=1");
        Assert.True(history.GetProperty("hasMore").GetBoolean());
        Assert.Equal(secondId, history.GetProperty("messages")[0].GetProperty("id").GetInt64());
        var older = await freshRequester.GetFromJsonAsync<System.Text.Json.JsonElement>($"{path}/messages?before={secondId}");
        Assert.Equal(firstId, older.GetProperty("messages")[0].GetProperty("id").GetInt64());
        var newer = await provider.GetFromJsonAsync<System.Text.Json.JsonElement>($"{path}/messages?after={firstId}");
        Assert.Equal(secondId, newer.GetProperty("messages")[0].GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.BadRequest, (await provider.PostAsJsonAsync($"{path}/receipt",
            new { deliveredThrough = secondId + 100, readThrough = secondId + 100 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await provider.PostAsJsonAsync($"{path}/receipt",
            new { deliveredThrough = secondId, readThrough = firstId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await provider.PostAsJsonAsync($"{path}/receipt",
            new { deliveredThrough = firstId, readThrough = 0 })).StatusCode);
        var receiptHistory = await freshRequester.GetFromJsonAsync<System.Text.Json.JsonElement>($"{path}/messages");
        Assert.Equal(secondId, receiptHistory.GetProperty("provider").GetProperty("deliveredThrough").GetInt64());
        Assert.Equal(firstId, receiptHistory.GetProperty("provider").GetProperty("readThrough").GetInt64());
        Assert.Equal(requesterId, receiptHistory.GetProperty("messages")[0].GetProperty("senderId").GetGuid());
        Assert.Equal("Weed the garden on Saturday morning.",
            (await requester.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/agreements/{id}")).GetProperty("terms").GetString());
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<ConnectoDbContext>().ConversationMessages.CountAsync());
    }

    private sealed class FixedCurrentUser(User user) : ICurrentUser
    {
        public Task<User> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(user);
    }

    [Fact]
    public async Task Media_quarantine_parent_authorisation_hidden_delivery_withdrawal_and_failure()
    {
        using var factory = new ApiFactory();
        using var requester = factory.CreateClient();
        using var provider = factory.CreateClient();
        using var outsider = factory.CreateClient();
        using var staff = factory.CreateClient();
        requester.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "media-requester"));
        provider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "media-provider"));
        outsider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "media-outsider"));
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "media-staff", group: "safety-operators"));
        var requesterId = (await requester.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/users/me")).GetProperty("id").GetGuid();
        var providerId = (await provider.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/users/me")).GetProperty("id").GetGuid();
        var agreementId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            db.Engagements.Add(new Engagement { Id = agreementId, RequesterId = requesterId, ProviderId = providerId,
                OutcomeState = "both_confirmed", OutcomeUpdatedAt = DateTime.UtcNow,
                ConnectionRequestId = Guid.NewGuid(), NeedId = Guid.NewGuid(), OfferingId = Guid.NewGuid() });
            await db.SaveChangesAsync();
        }
        var grantBody = new { agreementId, purpose = "review", kind = "before", contentType = "image/jpeg" };
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsJsonAsync("/api/media/upload-grants", grantBody)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await requester.PostAsJsonAsync("/api/media/upload-grants",
            new { agreementId, purpose = "review", kind = "before", contentType = "image/tiff" })).StatusCode);
        var badGrant = await requester.PostAsJsonAsync("/api/media/upload-grants", grantBody);
        Assert.Equal(HttpStatusCode.Created, badGrant.StatusCode);
        var badId = (await badGrant.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await requester.GetAsync($"/api/media/{badId}/content")).StatusCode);
        factory.Media.Put($"quarantine/{badId:N}", [1, 2, 3]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await requester.PostAsync($"/api/media/{badId}/finalise", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await requester.GetAsync($"/api/media/{badId}/content")).StatusCode);

        var goodGrant = await requester.PostAsJsonAsync("/api/media/upload-grants", grantBody);
        var goodId = (await goodGrant.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        using var bitmap = new SKBitmap(12, 8);
        bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        var baseBytes = jpeg.ToArray();
        var marker = new byte[] { 69, 120, 105, 102, 0, 0, 73, 73, 42, 0, 8, 0, 0, 0,
            1, 0, 18, 1, 3, 0, 1, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0 }
            .Concat(System.Text.Encoding.ASCII.GetBytes("GPS_SECRET_MARKER")).ToArray();
        var segment = new byte[] { 0xff, 0xe1, (byte)((marker.Length + 2) >> 8), (byte)(marker.Length + 2) };
        var bytes = baseBytes[..2].Concat(segment).Concat(marker).Concat(baseBytes[2..]).ToArray();
        var mismatchGrant = await requester.PostAsJsonAsync("/api/media/upload-grants",
            new { agreementId, purpose = "review", kind = "after", contentType = "image/png" });
        var mismatchId = (await mismatchGrant.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        factory.Media.Put($"quarantine/{mismatchId:N}", baseBytes);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await requester.PostAsync($"/api/media/{mismatchId}/finalise", null)).StatusCode);
        var oversizedGrant = await requester.PostAsJsonAsync("/api/media/upload-grants", grantBody);
        var oversizedId = (await oversizedGrant.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        factory.Media.Put($"quarantine/{oversizedId:N}", new byte[5 * 1024 * 1024 + 1]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await requester.PostAsync($"/api/media/{oversizedId}/finalise", null)).StatusCode);
        factory.Media.Put($"quarantine/{goodId:N}", bytes);
        var finalised = await requester.PostAsync($"/api/media/{goodId}/finalise", null);
        Assert.True(finalised.StatusCode == HttpStatusCode.OK, await finalised.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await provider.GetAsync($"/api/media/{goodId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/media/{goodId}/content")).StatusCode);
        var ownImage = await requester.GetAsync($"/api/media/{goodId}/content");
        Assert.Equal(HttpStatusCode.OK, ownImage.StatusCode);
        var servedBytes = await ownImage.Content.ReadAsByteArrayAsync();
        Assert.DoesNotContain("GPS_SECRET_MARKER", System.Text.Encoding.Latin1.GetString(servedBytes));
        using var servedData = SKData.CreateCopy(servedBytes);
        using var servedCodec = SKCodec.Create(servedData);
        Assert.Equal(8, servedCodec.Info.Width);
        Assert.Equal(12, servedCodec.Info.Height);
        Assert.True(ownImage.Headers.CacheControl?.NoStore);
        Assert.True(ownImage.Headers.CacheControl?.Private);
        var reviewBody = new { reliability = 5, communication = 4, publicationDelayDays = 30, assetIds = new[] { goodId } };
        Assert.Equal(HttpStatusCode.BadRequest, (await provider.PostAsJsonAsync($"/api/agreements/{agreementId}/reviews",
            new { communication = 4, publicationDelayDays = 1, assetIds = new[] { goodId } })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await requester.PostAsJsonAsync($"/api/agreements/{agreementId}/reviews", reviewBody)).StatusCode);
        Assert.Empty((await provider.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/media/agreements/{agreementId}")).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await provider.GetAsync($"/api/media/{goodId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await provider.PostAsJsonAsync($"/api/media/{goodId}/report",
            new { reason = "misleading", details = "Wrong outcome" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await provider.PostAsJsonAsync($"/api/agreements/{agreementId}/reviews",
            new { communication = 4, clarity = 5, publicationDelayDays = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await provider.GetAsync($"/api/media/{goodId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await provider.PostAsJsonAsync($"/api/media/{goodId}/report",
            new { reason = "misleading", details = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await provider.PostAsJsonAsync($"/api/media/{goodId}/report",
            new { reason = "misleading", details = "Wrong outcome" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await provider.PostAsJsonAsync($"/api/media/{goodId}/exclude",
            new { reason = "No" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await requester.PostAsync($"/api/agreements/{agreementId}/reviews/mine/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await provider.GetAsync($"/api/media/{goodId}/content")).StatusCode);

        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            var agreement = await db.Engagements.SingleAsync(x => x.Id == agreementId);
            agreement.OutcomeState = "disputed";
            await db.SaveChangesAsync();
        }
        var caseGrant = await requester.PostAsJsonAsync("/api/media/upload-grants",
            new { agreementId, purpose = "case", kind = "evidence", contentType = "image/jpeg" });
        Assert.Equal(HttpStatusCode.Created, caseGrant.StatusCode);
        var caseId = (await caseGrant.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        factory.Media.Put($"quarantine/{caseId:N}", baseBytes);
        Assert.Equal(HttpStatusCode.OK, (await requester.PostAsync($"/api/media/{caseId}/finalise", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await provider.GetAsync($"/api/media/{caseId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/media/{caseId}/content")).StatusCode);
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            Assert.Single(await db.MediaAccessAudits.Where(x => x.MediaAssetId == caseId).ToListAsync());
            Assert.Single(await db.SafetyReports.Where(x => x.MediaAssetId == goodId).ToListAsync());
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await provider.GetAsync($"/api/media/agreements/{agreementId}/case-evidence")).StatusCode);
        Assert.Single((await staff.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/media/agreements/{agreementId}/case-evidence")).EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await requester.PostAsync($"/api/media/{caseId}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/api/media/{caseId}/content")).StatusCode);
    }

    [Fact]
    public async Task Reviews_enforce_eligibility_hidden_publication_roles_and_reputation_recalculation()
    {
        using var factory = new ApiFactory();
        using var requester = factory.CreateClient();
        using var provider = factory.CreateClient();
        using var outsider = factory.CreateClient();
        using var operatorClient = factory.CreateClient();
        requester.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "review-requester"));
        provider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "review-provider"));
        outsider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "review-outsider"));
        operatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            factory.Token(subject: "review-operator", group: "safety-operators"));
        var requesterId = (await requester.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/users/me")).GetProperty("id").GetGuid();
        var providerId = (await provider.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/users/me")).GetProperty("id").GetGuid();
        var agreementId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            db.Engagements.Add(new Engagement {
                Id = agreementId, RequesterId = requesterId, ProviderId = providerId,
                OutcomeState = "awaiting_confirmation", OutcomeUpdatedAt = DateTime.UtcNow,
                ConnectionRequestId = Guid.NewGuid(), NeedId = Guid.NewGuid(), OfferingId = Guid.NewGuid()
            });
            await db.SaveChangesAsync();
        }
        var path = $"/api/agreements/{agreementId}";
        var requesterBody = new { reliability = 5, communication = 4, agreement = 3,
            comment = "Helpful provider", publicationDelayDays = 30 };
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"{path}/review-eligibility")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"{path}/reviews")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await requester.PostAsJsonAsync($"{path}/reviews", requesterBody)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            var agreement = await db.Engagements.SingleAsync(x => x.Id == agreementId);
            agreement.OutcomeState = "both_confirmed";
            agreement.OutcomeUpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await provider.PostAsJsonAsync($"{path}/reviews", requesterBody)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await requester.PostAsJsonAsync($"{path}/reviews",
            new { reliability = 5, publicationDelayDays = 31 })).StatusCode);
        var first = await requester.PostAsJsonAsync($"{path}/reviews", requesterBody);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await requester.PostAsJsonAsync($"{path}/reviews", requesterBody)).StatusCode);
        var hidden = await provider.GetFromJsonAsync<System.Text.Json.JsonElement>($"{path}/reviews");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, hidden.GetProperty("reciprocal").ValueKind);
        Assert.Equal(0, (await provider.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/members/{providerId}/reputation"))
            .GetProperty("asProvider").GetProperty("reviewCount").GetInt32());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            var review = await db.EngagementReviews.SingleAsync(x => x.Id == firstId);
            review.PublishAfterAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        var afterOwnDeadline = await provider.GetFromJsonAsync<System.Text.Json.JsonElement>($"{path}/reviews");
        Assert.Equal("Helpful provider", afterOwnDeadline.GetProperty("reciprocal").GetProperty("comment").GetString());
        Assert.Equal(1, (await provider.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/members/{providerId}/reputation"))
            .GetProperty("asProvider").GetProperty("reviewCount").GetInt32());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            var agreement = await db.Engagements.SingleAsync(x => x.Id == agreementId);
            agreement.OutcomeUpdatedAt = DateTime.UtcNow.AddDays(-31);
            await db.SaveChangesAsync();
        }
        var expired = await provider.GetFromJsonAsync<System.Text.Json.JsonElement>($"{path}/review-eligibility");
        Assert.False(expired.GetProperty("eligible").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await provider.PostAsJsonAsync($"{path}/reviews", new {
            communication = 2, publicationDelayDays = 1
        })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            var agreement = await db.Engagements.SingleAsync(x => x.Id == agreementId);
            agreement.OutcomeUpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await requester.PostAsJsonAsync($"/api/reviews/{firstId}/exclude",
            new { reason = "No" })).StatusCode);
        var second = await provider.PostAsJsonAsync($"{path}/reviews", new {
            communication = 2, clarity = 4, commitments = 5, comment = "Clear brief", publicationDelayDays = 1
        });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var published = await provider.GetFromJsonAsync<System.Text.Json.JsonElement>($"{path}/reviews");
        Assert.Equal("Helpful provider", published.GetProperty("reciprocal").GetProperty("comment").GetString());
        var reputation = await provider.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/members/{providerId}/reputation");
        Assert.Equal(1, reputation.GetProperty("asProvider").GetProperty("reviewCount").GetInt32());
        Assert.Equal(5, reputation.GetProperty("asProvider").GetProperty("dimensions")
            .GetProperty("reliability").GetProperty("average").GetDouble());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            var agreement = await db.Engagements.SingleAsync(x => x.Id == agreementId);
            agreement.OutcomeState = "disputed";
            await db.SaveChangesAsync();
        }
        Assert.Equal(System.Text.Json.JsonValueKind.Null,
            (await provider.GetFromJsonAsync<System.Text.Json.JsonElement>($"{path}/reviews")).GetProperty("reciprocal").ValueKind);
        Assert.Equal(0, (await provider.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/members/{providerId}/reputation"))
            .GetProperty("asProvider").GetProperty("reviewCount").GetInt32());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            var agreement = await db.Engagements.SingleAsync(x => x.Id == agreementId);
            agreement.OutcomeState = "both_confirmed";
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await requester.PostAsync($"{path}/reviews/mine/withdraw", null)).StatusCode);
        Assert.Equal(0, (await provider.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/members/{providerId}/reputation"))
            .GetProperty("asProvider").GetProperty("reviewCount").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await requester.PostAsJsonAsync($"{path}/reviews", requesterBody)).StatusCode);
        var secondId = (await second.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await operatorClient.PostAsJsonAsync($"/api/reviews/{secondId}/exclude",
            new { reason = "Policy review" })).StatusCode);
        Assert.Equal(0, (await requester.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/members/{requesterId}/reputation"))
            .GetProperty("asRequester").GetProperty("reviewCount").GetInt32());
        var hiddenAgreementId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            db.Engagements.Add(new Engagement {
                Id = hiddenAgreementId, RequesterId = requesterId, ProviderId = providerId,
                OutcomeState = "both_confirmed", OutcomeUpdatedAt = DateTime.UtcNow,
                ConnectionRequestId = Guid.NewGuid(), NeedId = Guid.NewGuid(), OfferingId = Guid.NewGuid()
            });
            await db.SaveChangesAsync();
        }
        var hiddenPath = $"/api/agreements/{hiddenAgreementId}/reviews";
        Assert.Equal(HttpStatusCode.Created, (await requester.PostAsJsonAsync(hiddenPath, requesterBody)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await requester.PostAsync($"{hiddenPath}/mine/withdraw", null)).StatusCode);
        Assert.Equal(System.Text.Json.JsonValueKind.Null,
            (await provider.GetFromJsonAsync<System.Text.Json.JsonElement>(hiddenPath)).GetProperty("reciprocal").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await requester.PostAsJsonAsync(hiddenPath, requesterBody)).StatusCode);
    }

    [Fact]
    public async Task Independent_outcomes_keep_history_and_reject_stale_or_unauthorised_actions()
    {
        using var factory = new ApiFactory();
        using var requester = factory.CreateClient();
        using var provider = factory.CreateClient();
        using var outsider = factory.CreateClient();
        requester.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "outcome-requester"));
        provider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "outcome-provider"));
        outsider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "outcome-outsider"));
        var need = await requester.PostAsJsonAsync("/api/needs", new {
            title = "Outcome need", description = "Outcome acceptance", category = "Technology",
            skills = new[] { "Programming" }, currency = "NZD", pricingBasis = "free"
        });
        var offer = await provider.PostAsJsonAsync("/api/offerings", new {
            title = "Outcome offer", description = "Outcome acceptance", category = "Technology",
            skills = new[] { "Programming" }, location = "Auckland", currency = "NZD", pricingBasis = "free", experienceYears = 0
        });
        Assert.Equal(HttpStatusCode.Created, need.StatusCode);
        Assert.Equal(HttpStatusCode.Created, offer.StatusCode);
        var needId = (await need.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        var offerId = (await offer.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        var proposed = await requester.PostAsJsonAsync("/api/connection-requests", new {
            needId, offeringId = offerId, needRevision = 1, offeringRevision = 1,
            terms = "Outcome work terms", pricingBasis = "free"
        });
        Assert.Equal(HttpStatusCode.Created, proposed.StatusCode);
        var requestId = (await proposed.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        var accepted = await provider.PostAsJsonAsync($"/api/connection-requests/{requestId}/accept",
            new { revision = 1, idempotencyKey = "outcome-accept" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var id = (await accepted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        var path = $"/api/agreements/{id}/outcome";
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsJsonAsync($"{path}/report",
            new { revision = 1, idempotencyKey = "outside", outcome = "completed" })).StatusCode);
        var firstBody = new { revision = 1, idempotencyKey = "requester-report", outcome = "completed" };
        var first = await requester.PostAsJsonAsync($"{path}/report", firstBody);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("one_reported", (await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("outcomeState").GetString());
        Assert.Equal(HttpStatusCode.OK, (await requester.PostAsJsonAsync($"{path}/report", firstBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await provider.PostAsJsonAsync($"{path}/report",
            new { revision = 1, idempotencyKey = "stale", outcome = "completed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await requester.PostAsJsonAsync($"{path}/report",
            new { revision = 2, idempotencyKey = "duplicate", outcome = "completed" })).StatusCode);
        var second = await provider.PostAsJsonAsync($"{path}/report",
            new { revision = 2, idempotencyKey = "provider-report", outcome = "completed" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var both = await second.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("both_confirmed", both.GetProperty("outcomeState").GetString());
        Assert.Equal(2, both.GetProperty("history").GetArrayLength());
        Assert.Equal(HttpStatusCode.Conflict, (await requester.PostAsJsonAsync($"{path}/cancel",
            new { revision = 3, idempotencyKey = "late-cancel", reason = "No longer needed" })).StatusCode);
        var dispute = await requester.PostAsJsonAsync($"{path}/dispute",
            new { revision = 3, idempotencyKey = "dispute", reason = "Completion is contested" });
        Assert.Equal(HttpStatusCode.OK, dispute.StatusCode);
        Assert.Equal("disputed", (await dispute.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("outcomeState").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync("/api/outcome-disputes")).StatusCode);
        using var operatorClient = factory.CreateClient();
        operatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            factory.Token(subject: "outcome-operator", group: "safety-operators"));
        var queue = await operatorClient.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/outcome-disputes");
        Assert.Contains(queue.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
        var reviewed = await operatorClient.PostAsJsonAsync($"/api/outcome-disputes/{id}/review",
            new { note = "Reviewed and referred to support" });
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await operatorClient.PostAsJsonAsync($"/api/outcome-disputes/{id}/review",
            new { note = "Duplicate review" })).StatusCode);
        using var fresh = factory.CreateClient();
        fresh.DefaultRequestHeaders.Authorization = requester.DefaultRequestHeaders.Authorization;
        var saved = await fresh.GetFromJsonAsync<System.Text.Json.JsonElement>(path);
        Assert.Equal(4, saved.GetProperty("history").GetArrayLength());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, saved.GetProperty("disputeReviewedAt").ValueKind);
    }

    [Fact]
    public async Task Pilot_api_journey_reaches_both_confirmed_feedback_and_measured_cohort()
    {
        using var factory = new ApiFactory();
        using var requester = factory.CreateClient();
        using var provider = factory.CreateClient();
        using var outsider = factory.CreateClient();
        using var staff = factory.CreateClient();
        requester.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "pilot-journey-requester"));
        provider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "pilot-journey-provider"));
        outsider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "pilot-journey-outsider"));
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "pilot-journey-staff", group: "pilot-viewers"));
        var needResponse = await requester.PostAsJsonAsync("/api/needs", new CreateNeedRequest(
            "Garden help", "Need weeding", "Home and Garden", ["Gardening"], null, null,
            "Auckland", null, "NZD", "negotiable"));
        var offerResponse = await provider.PostAsJsonAsync("/api/offerings", new CreateOfferingRequest(
            "Garden help", "Weeding available", "Home and Garden", ["Gardening"], null, null,
            "Auckland", false, null, null, 0, "NZD", "negotiable"));
        Assert.Equal(HttpStatusCode.Created, needResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, offerResponse.StatusCode);
        var needId = (await needResponse.Content.ReadFromJsonAsync<Listing>())!.Id;
        var offeringId = (await offerResponse.Content.ReadFromJsonAsync<Listing>())!.Id;
        Assert.Equal(HttpStatusCode.OK, (await requester.GetAsync($"/api/needs/{needId}/matches")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await requester.PostAsJsonAsync("/api/pilot/signals/discovery",
            new { needId, clientKey = "journey-search" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await requester.PostAsJsonAsync("/api/pilot/signals/suitable",
            new { needId, offeringId, clientKey = "journey-suitable" })).StatusCode);
        var proposal = await requester.PostAsJsonAsync("/api/connection-requests", new {
            needId, offeringId, needRevision = 1, offeringRevision = 1,
            terms = "Weed the garden on Saturday morning.", pricingBasis = "free"
        });
        Assert.Equal(HttpStatusCode.Created, proposal.StatusCode);
        var requestId = (await proposal.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        var accepted = await provider.PostAsJsonAsync($"/api/connection-requests/{requestId}/accept",
            new { revision = 1, idempotencyKey = "journey-accept" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var agreementId = (await accepted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/agreements/{agreementId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await requester.PostAsJsonAsync(
            $"/api/agreements/{agreementId}/conversation/messages",
            new { text = "Saturday works.", idempotencyKey = "journey-message" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(
            $"/api/agreements/{agreementId}/conversation/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await requester.PostAsJsonAsync(
            $"/api/agreements/{agreementId}/outcome/report",
            new { revision = 1, idempotencyKey = "journey-requester-outcome", outcome = "completed" })).StatusCode);
        var secondOutcome = await provider.PostAsJsonAsync($"/api/agreements/{agreementId}/outcome/report",
            new { revision = 2, idempotencyKey = "journey-provider-outcome", outcome = "completed" });
        Assert.Equal(HttpStatusCode.OK, secondOutcome.StatusCode);
        Assert.Equal("both_confirmed", (await secondOutcome.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())
            .GetProperty("outcomeState").GetString());
        Assert.Equal(HttpStatusCode.Created, (await requester.PostAsJsonAsync($"/api/agreements/{agreementId}/reviews",
            new { reliability = 5, communication = 4, agreement = 5, publicationDelayDays = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await provider.PostAsJsonAsync($"/api/agreements/{agreementId}/reviews",
            new { communication = 4, clarity = 5, commitments = 5, publicationDelayDays = 1 })).StatusCode);
        var from = Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(-5).ToString("O"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(1).ToString("O"));
        var path = $"/api/pilot/metrics?from={from}&to={to}";
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(path)).StatusCode);
        var report = await staff.GetFromJsonAsync<System.Text.Json.JsonElement>(path);
        Assert.Equal(1, report.GetProperty("discovery").GetProperty("coveredNeeds").GetInt32());
        Assert.Equal(1, report.GetProperty("suitability").GetProperty("markedNeeds").GetInt32());
        Assert.Equal(1, report.GetProperty("acceptance").GetProperty("numerator").GetInt32());
        Assert.Equal(1, report.GetProperty("completion").GetProperty("numerator").GetInt32());
        Assert.Equal(2, report.GetProperty("feedback").GetProperty("submittedParticipantSlots").GetInt32());
    }

    [Fact]
    public async Task Pilot_metrics_require_group_and_count_distinct_saved_outcomes()
    {
        using var factory = new ApiFactory();
        using var member = factory.CreateClient();
        member.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        var from = DateTime.UtcNow.AddDays(-1);
        var to = DateTime.UtcNow.AddMinutes(1);
        var path = $"/api/pilot/metrics?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync(path)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
        var request = new ConnectionRequest { SenderId = Guid.NewGuid(), RecipientId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddHours(-1) };
        var accepted = new Engagement { ConnectionRequestId = request.Id, RequesterId = request.SenderId,
            ProviderId = request.RecipientId, AcceptedAt = DateTime.UtcNow, OutcomeState = "both_confirmed" };
        var otherRequests = Enumerable.Range(0, 4).Select(_ => new ConnectionRequest {
            SenderId = request.SenderId, RecipientId = request.RecipientId, CreatedAt = DateTime.UtcNow.AddMinutes(-30)
        }).ToArray();
        db.ConnectionRequests.Add(request);
        db.ConnectionRequests.AddRange(otherRequests);
        db.Engagements.Add(accepted);
        db.Engagements.AddRange(new[] { "one_reported", "cancelled", "disputed" }.Select((state, index) =>
            new Engagement { ConnectionRequestId = otherRequests[index].Id, RequesterId = request.SenderId,
                ProviderId = request.RecipientId, OutcomeState = state }));
        db.EngagementOutcomeEvents.Add(new EngagementOutcomeEvent { EngagementId = accepted.Id,
            ActorId = request.SenderId, Action = "report", ReportedOutcome = "completed" });
        db.EngagementOutcomeEvents.Add(new EngagementOutcomeEvent { EngagementId = accepted.Id,
            ActorId = request.RecipientId, Action = "report", ReportedOutcome = "completed" });
        db.EngagementReviews.Add(new EngagementReview { EngagementId = accepted.Id,
            ReviewerId = request.SenderId, RevieweeId = request.RecipientId, ReviewerRole = "requester",
            Reliability = 4, Communication = 5, PublishedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        using var staff = factory.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(group: "safety-operators"));
        var report = await staff.GetFromJsonAsync<System.Text.Json.JsonElement>(path);
        Assert.Equal(5, report.GetProperty("cohort").GetProperty("requestCount").GetInt32());
        Assert.Equal(4, report.GetProperty("acceptance").GetProperty("numerator").GetInt32());
        Assert.Equal(1, report.GetProperty("completion").GetProperty("numerator").GetInt32());
        Assert.Equal(1, report.GetProperty("completion").GetProperty("oneReported").GetInt32());
        Assert.Equal(1, report.GetProperty("completion").GetProperty("cancelled").GetInt32());
        Assert.Equal(1, report.GetProperty("completion").GetProperty("disputed").GetInt32());
        Assert.Equal(1, report.GetProperty("feedback").GetProperty("submittedParticipantSlots").GetInt32());
        Assert.Equal(2, report.GetProperty("feedback").GetProperty("eligibleParticipantSlots").GetInt32());
        Assert.Equal(2, report.GetProperty("feedback").GetProperty("publishedRatingCount").GetInt32());
        Assert.Equal(2, report.GetProperty("repeatUse").GetProperty("numerator").GetInt32());
        using var pilotViewer = factory.CreateClient();
        pilotViewer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(group: "pilot-viewers"));
        Assert.Equal(HttpStatusCode.OK, (await pilotViewer.GetAsync(path)).StatusCode);
        using var wrongGroup = factory.CreateClient();
        wrongGroup.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(group: "other-staff"));
        Assert.Equal(HttpStatusCode.Forbidden, (await wrongGroup.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.GetAsync("/api/pilot/metrics?from=2026-10-10T00:00:00Z&to=2026-10-09T00:00:00Z")).StatusCode);
    }

    [Fact]
    public async Task Pilot_signals_are_owned_idempotent_and_excluded_from_demo_free_cohorts()
    {
        using var factory = new ApiFactory();
        using var requester = factory.CreateClient();
        requester.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "pilot-requester"));
        var needResponse = await requester.PostAsJsonAsync("/api/needs", new CreateNeedRequest(
            "Garden help", "Need some weeding", "Home and Garden", ["Gardening"], null, null,
            "Auckland", null, "NZD", "negotiable"));
        Assert.Equal(HttpStatusCode.Created, needResponse.StatusCode);
        var need = await needResponse.Content.ReadFromJsonAsync<Listing>();
        var secondNeedResponse = await requester.PostAsJsonAsync("/api/needs", new CreateNeedRequest(
            "Lawn help", "Need lawn mowing", "Home and Garden", ["Gardening"], null, null,
            "Auckland", null, "NZD", "negotiable"));
        Assert.Equal(HttpStatusCode.Created, secondNeedResponse.StatusCode);
        var secondNeed = await secondNeedResponse.Content.ReadFromJsonAsync<Listing>();
        using var provider = factory.CreateClient();
        provider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "pilot-provider"));
        var offeringResponse = await provider.PostAsJsonAsync("/api/offerings", new CreateOfferingRequest(
            "Garden help", "Can help with weeding", "Home and Garden", ["Gardening"], null, null,
            "Auckland", false, null, null, 0, "NZD", "negotiable"));
        Assert.Equal(HttpStatusCode.Created, offeringResponse.StatusCode);
        var offering = await offeringResponse.Content.ReadFromJsonAsync<Listing>();
        var discovery = new { needId = need!.Id, clientKey = "view-one" };
        Assert.Equal(HttpStatusCode.NotFound,
            (await provider.PostAsJsonAsync("/api/pilot/signals/discovery", discovery)).StatusCode);
        var first = await requester.PostAsJsonAsync("/api/pilot/signals/discovery", discovery);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await requester.PostAsJsonAsync("/api/pilot/signals/discovery", discovery)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await requester.PostAsJsonAsync("/api/pilot/signals/discovery", new { needId = secondNeed!.Id, clientKey = "view-one" })).StatusCode);
        var suitable = new { needId = need.Id, offeringId = offering!.Id, clientKey = "suitable-one" };
        Assert.Equal(HttpStatusCode.NotFound,
            (await provider.PostAsJsonAsync("/api/pilot/signals/suitable", suitable)).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await requester.PostAsJsonAsync("/api/pilot/signals/suitable", suitable)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await requester.PostAsJsonAsync("/api/pilot/signals/suitable", suitable)).StatusCode);
        var ineligibleResponse = await provider.PostAsJsonAsync("/api/offerings", new CreateOfferingRequest(
            "Garden help elsewhere", "Can help with weeding elsewhere", "Home and Garden", ["Gardening"], null, null,
            "Wellington", false, null, null, 0, "NZD", "negotiable"));
        Assert.Equal(HttpStatusCode.Created, ineligibleResponse.StatusCode);
        var ineligibleOffering = await ineligibleResponse.Content.ReadFromJsonAsync<Listing>();
        Assert.Equal(HttpStatusCode.Conflict,
            (await requester.PostAsJsonAsync("/api/pilot/signals/suitable", new {
                needId = need.Id, offeringId = ineligibleOffering!.Id, clientKey = "suitable-ineligible"
            })).StatusCode);
        using var staff = factory.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(group: "safety-operators"));
        var from = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(1).ToString("O"));
        var report = await staff.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/pilot/metrics?from={from}&to={to}");
        Assert.Equal(1, report.GetProperty("discovery").GetProperty("searchedNeeds").GetInt32());
        Assert.Equal(1, report.GetProperty("suitability").GetProperty("markedNeedOfferingPairs").GetInt32());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            var storedNeed = await db.Needs.SingleAsync(x => x.Id == need.Id);
            storedNeed.Status = NeedStatus.Paused;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK,
            (await requester.PostAsJsonAsync("/api/pilot/signals/discovery", discovery)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await requester.PostAsJsonAsync("/api/pilot/signals/discovery", new {
                needId = need.Id, clientKey = "new-after-pause"
            })).StatusCode);
    }

    private sealed class ApiFactory(string apiKey = "") : WebApplicationFactory<Program>
    {
        public FakeMediaStore Media { get; } = new();
        private readonly RSA rsa = RSA.Create(2048);
        private readonly string database = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Cognito:Issuer", "https://test-issuer.example");
            builder.UseSetting("Cognito:Audience", "test-client");
            builder.UseSetting("OpenAI:ApiKey", apiKey);
            builder.UseSetting("Safety:OperatorGroup", "safety-operators");
            builder.UseSetting("Pilot:ViewerGroup", "pilot-viewers");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ConnectoDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ConnectoDbContext>>();
                services.AddDbContext<ConnectoDbContext>(options => options.UseInMemoryDatabase(database));
                services.RemoveAll<IMediaObjectStore>();
                services.AddSingleton<IMediaObjectStore>(Media);
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var configuration = new OpenIdConnectConfiguration { Issuer = "https://test-issuer.example" };
                    configuration.SigningKeys.Add(new RsaSecurityKey(rsa) { KeyId = "test-key" });
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
            });
        }

        public string Token(string use = "access", string clientId = "test-client", string issuer = "https://test-issuer.example", bool expired = false, bool wrongKey = false, string subject = "subject-1", string? group = null)
        {
            using var other = RSA.Create(2048);
            var key = new RsaSecurityKey(wrongKey ? other : rsa) { KeyId = "test-key" };
            var claims = new List<Claim> { new("sub", subject), new("token_use", use), new("client_id", clientId) };
            if (group is not null) claims.Add(new Claim("cognito:groups", group));
            var token = new JwtSecurityToken(issuer, claims: claims,
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

    private sealed class FakeMediaStore : IMediaObjectStore
    {
        public bool IsAvailable => true;
        private readonly ConcurrentDictionary<string, byte[]> objects = new();
        public void Put(string key, byte[] bytes) => objects[key] = bytes;
        public Task<(string Url, Dictionary<string, string> Fields)> UploadGrantAsync(string key, string contentType, DateTime expiresAt) =>
            Task.FromResult(("https://private.invalid/upload", new Dictionary<string, string> { ["key"] = key, ["Content-Type"] = contentType }));
        public Task<(Stream Body, long Length)> ReadAsync(string key, CancellationToken ct) {
            if (!objects.TryGetValue(key, out var bytes)) throw new Amazon.S3.AmazonS3Exception("Missing") { StatusCode = HttpStatusCode.NotFound };
            return Task.FromResult<(Stream, long)>((new MemoryStream(bytes), bytes.Length));
        }
        public async Task WriteAsync(string key, Stream body, string contentType, CancellationToken ct) {
            using var memory = new MemoryStream(); await body.CopyToAsync(memory, ct); objects[key] = memory.ToArray();
        }
        public Task DeleteAsync(string key, CancellationToken ct) { objects.TryRemove(key, out _); return Task.CompletedTask; }
    }
}
