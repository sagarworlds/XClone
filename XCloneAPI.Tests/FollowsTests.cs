using System.Net;
using System.Net.Http.Json;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

[Collection(ApiCollection.Name)]
public class FollowsTests(ApiFixture api)
{
    [Fact]
    public async Task Toggle_FollowsThenUnfollows()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");

        var on = await bob.PostAsync($"/api/follows/toggle/{alice.Id}");
        Assert.True((await on.ReadAsync<Dictionary<string, bool>>())["followed"]);
        Assert.True((await bob.GetAsync<Dictionary<string, bool>>($"/api/follows/is-following/{alice.Id}"))["isFollowing"]);

        var off = await bob.PostAsync($"/api/follows/toggle/{alice.Id}");
        Assert.False((await off.ReadAsync<Dictionary<string, bool>>())["followed"]);
        Assert.False((await bob.GetAsync<Dictionary<string, bool>>($"/api/follows/is-following/{alice.Id}"))["isFollowing"]);
        Assert.Equal(0, (await alice.GetAsync<UserResponse>("/api/users/profile")).FollowersCount);
    }

    [Fact]
    public async Task YouCannotFollowYourself_OrAUserWhoDoesNotExist()
    {
        var alice = await api.RegisterAsync("alice");

        var self = await alice.PostAsync($"/api/follows/toggle/{alice.Id}");
        await self.ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.Equal("Cannot follow yourself", await self.ReadMessageAsync());

        var missing = await alice.PostAsync("/api/follows/toggle/99999999");
        await missing.ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.Equal("User not found", await missing.ReadMessageAsync());
    }

    [Fact]
    public async Task Following_RequiresLogin()
    {
        var alice = await api.RegisterAsync("alice");

        await (await api.Anonymous.PostAsJsonAsync($"/api/follows/toggle/{alice.Id}", new { })).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await api.Anonymous.GetAsync($"/api/follows/is-following/{alice.Id}")).ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FollowerAndFollowingLists_AreAvailableToEveryone_WithoutEmails()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        await bob.FollowAsync(alice);
        await carol.FollowAsync(alice);
        await alice.FollowAsync(bob);

        var followers = await api.Anonymous.GetItemsAsync<UserResponse>($"/api/users/{alice.Id}/followers");
        Assert.Equal(new[] { bob.Id, carol.Id }.Order(), followers.Select(u => u.Id).Order());

        var following = await api.Anonymous.GetItemsAsync<UserResponse>($"/api/users/{alice.Id}/following");
        Assert.Equal(new[] { bob.Id }, following.Select(u => u.Id));

        Assert.All(followers.Concat(following), u => Assert.True(string.IsNullOrEmpty(u.Email)));
    }

    // Paging, order and changes between pages are covered by FollowListTests.
}
