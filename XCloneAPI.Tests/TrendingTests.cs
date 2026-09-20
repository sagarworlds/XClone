using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>
/// GET /api/hashtags/trending: the tags most used by posts of the last week. Each test has a database of its own,
/// because "the most used tags" would otherwise depend on everything the other tests posted.
/// </summary>
public sealed class TrendingTests : IAsyncLifetime
{
    private readonly TestDatabase _database = new();
    private ApiFactory _factory = null!;
    private HttpClient _anonymous = null!;

    public async Task InitializeAsync()
    {
        await _database.CreateAsync();
        _factory = ApiFactory.ForDatabase(_database.ConnectionString);
        _anonymous = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _anonymous.Dispose();
        await _factory.DisposeAsync();
        await _database.DropAsync();
    }

    private Task<TestUser> Register(string name) => TestUser.RegisterAsync(_factory, name, "Passw0rd!x");

    private async Task<List<TrendingHashtagResponse>> Trending(string query = "")
    {
        var response = await _anonymous.GetAsync("/api/hashtags/trending" + query);
        await response.ShouldBeAsync(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<TrendingHashtagResponse>>(TestUser.Json))!;
    }

    private static string[] Tags(IEnumerable<TrendingHashtagResponse> trending) => trending.Select(t => t.Tag).ToArray();

    private async Task MakeOlder(int postId, TimeSpan by)
    {
        await using var db = _database.CreateContext();
        await db.Posts.Where(p => p.Id == postId).ExecuteUpdateAsync(s => s.SetProperty(p => p.CreatedAt, DateTime.UtcNow - by));
    }

    [Fact]
    public async Task WithNoTagsAtAll_ItIsEmpty()
    {
        Assert.Empty(await Trending());
    }

    [Fact]
    public async Task TheMostUsedComeFirst_AndTiesAreInAlphabeticalOrder()
    {
        var alice = await Register("alice");
        var bob = await Register("bob");
        await alice.CreatePostAsync("#delta");
        foreach (var user in new[] { alice, bob }) { await user.CreatePostAsync("#beta"); await user.CreatePostAsync("#alpha"); }
        foreach (var text in new[] { "#gamma one", "#gamma two", "#gamma three" }) await bob.CreatePostAsync(text);

        var trending = await Trending();

        Assert.Equal(new[] { "gamma", "alpha", "beta", "delta" }, Tags(trending));
        Assert.Equal(new[] { 3, 2, 2, 1 }, trending.Select(t => t.PostsCount));
    }

    [Fact]
    public async Task ItCountsPosts_NotHowOftenAPostRepeatsATag()
    {
        var alice = await Register("alice");
        await alice.CreatePostAsync("#loud #loud #loud #LOUD");
        await alice.CreatePostAsync("#loud");
        await alice.CreatePostAsync("#quiet");
        await alice.CreatePostAsync("#quiet again");

        var trending = await Trending();

        Assert.Equal(new[] { 2, 2 }, trending.Select(t => t.PostsCount));
        Assert.Equal(new[] { "loud", "quiet" }, Tags(trending));
    }

    [Fact]
    public async Task RepliesCountToo()
    {
        var alice = await Register("alice");
        var bob = await Register("bob");
        var root = await alice.CreatePostAsync("no tag here");
        await bob.ReplyAsync(root.Id, "a reply #thread");
        await alice.CreatePostAsync("and a post #thread");

        Assert.Equal(2, Assert.Single(await Trending()).PostsCount);
    }

    [Fact]
    public async Task OnlyTheLastSevenDaysCount()
    {
        var alice = await Register("alice");
        var old = await alice.CreatePostAsync("#ancient");
        var almost = await alice.CreatePostAsync("#recent");
        await alice.CreatePostAsync("#today");
        await MakeOlder(old.Id, TimeSpan.FromDays(8));
        await MakeOlder(almost.Id, TimeSpan.FromDays(6.9));

        Assert.Equal(new[] { "recent", "today" }, Tags(await Trending()));
    }

    [Fact]
    public async Task ATagWhosePostsAreAllOld_IsGone_EvenWithManyPosts()
    {
        var alice = await Register("alice");
        for (var i = 0; i < 4; i++) await MakeOlder((await alice.CreatePostAsync("#stale")).Id, TimeSpan.FromDays(30));
        await alice.CreatePostAsync("#fresh");

        Assert.Equal(new[] { "fresh" }, Tags(await Trending()));
    }

    [Fact]
    public async Task DeletedPostsDoNotCount()
    {
        var alice = await Register("alice");
        await alice.CreatePostAsync("#kept");
        var doomed = await alice.CreatePostAsync("#kept too");
        var alone = await alice.CreatePostAsync("#gone");
        await (await alice.DeleteAsync($"/api/posts/{doomed.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);
        await (await alice.DeleteAsync($"/api/posts/{alone.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        var trending = await Trending();

        Assert.Equal(new[] { "kept" }, Tags(trending));
        Assert.Equal(1, trending[0].PostsCount);
    }

    [Fact]
    public async Task ItAsksForFiveByDefault_AndTheNumberIsClamped()
    {
        var alice = await Register("alice");
        for (var i = 1; i <= 25; i++) await alice.CreatePostAsync($"#tag{i:00}");

        Assert.Equal(5, (await Trending()).Count);
        Assert.Equal(2, (await Trending("?take=2")).Count);
        Assert.Single(await Trending("?take=0"));
        Assert.Single(await Trending("?take=-7"));
        Assert.Equal(20, (await Trending("?take=1000")).Count);
    }

    [Fact]
    public async Task AnyoneCanSeeIt_AndTagsAreLowerCaseWithoutTheHash()
    {
        var alice = await Register("alice");
        await alice.CreatePostAsync("Look #Sunset");

        var trending = await Trending();   // no login at all

        var only = Assert.Single(trending);
        Assert.Equal("sunset", only.Tag);
        Assert.Equal(1, only.PostsCount);
        var raw = await (await _anonymous.GetAsync("/api/hashtags/trending")).Content.ReadAsStringAsync();
        Assert.Contains("\"tag\":\"sunset\"", raw);
        Assert.Contains("\"postsCount\":1", raw);
    }

    [Fact]
    public async Task TheOrderIsStable_WhenNothingChanges()
    {
        var alice = await Register("alice");
        foreach (var tag in new[] { "zeta", "eta", "theta", "iota" }) await alice.CreatePostAsync("#" + tag);

        var first = Tags(await Trending());
        var second = Tags(await Trending());

        Assert.Equal(new[] { "eta", "iota", "theta", "zeta" }, first);
        Assert.Equal(first, second);
    }
}
