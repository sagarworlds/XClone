using System.Net;
using System.Net.Http.Json;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

[Collection(ApiCollection.Name)]
public class RetweetsTests(ApiFixture api)
{
    [Fact]
    public async Task Toggle_RepostsThenUndoes_AndKeepsTheCountAndFlagInSync()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("repost me");

        Assert.True(await bob.ToggleRetweetAsync(post.Id));
        var reposted = await bob.GetPostAsync(post.Id);
        Assert.Equal(1, reposted.RetweetsCount);
        Assert.True(reposted.IsRetweeted);
        Assert.False((await alice.GetPostAsync(post.Id)).IsRetweeted);   // per viewer

        Assert.False(await bob.ToggleRetweetAsync(post.Id));
        var undone = await bob.GetPostAsync(post.Id);
        Assert.Equal(0, undone.RetweetsCount);
        Assert.False(undone.IsRetweeted);
    }

    [Fact]
    public async Task ManyPeopleCanRepost_AndYouCanRepostYourOwnPost()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("popular");

        await bob.ToggleRetweetAsync(post.Id);
        await alice.ToggleRetweetAsync(post.Id);

        Assert.Equal(2, (await alice.GetPostAsync(post.Id)).RetweetsCount);
    }

    [Fact]
    public async Task ARepost_ShowsUpInFollowersFeeds_WithWhoReposted_AndTheOriginalAuthor()
    {
        var author = await api.RegisterAsync("author");
        var reposter = await api.RegisterAsync("reposter");
        var follower = await api.RegisterAsync("follower");
        await follower.FollowAsync(reposter);   // follows the reposter only, not the author
        var post = await author.CreatePostAsync("original content");

        Assert.Empty(await follower.FeedAsync());   // nothing yet

        await reposter.ToggleRetweetAsync(post.Id);
        var entry = Assert.Single(await follower.FeedAsync());

        Assert.Equal(post.Id, entry.Id);
        Assert.Equal(author.Username, entry.User.Username);
        Assert.NotNull(entry.RetweetedBy);
        Assert.Equal(reposter.Id, entry.RetweetedBy!.Id);
        Assert.Equal(reposter.Username, entry.RetweetedBy.Username);
        Assert.True(string.IsNullOrEmpty(entry.RetweetedBy.Email));
        Assert.Equal(1, entry.RetweetsCount);
    }

    [Fact]
    public async Task UndoingARepost_RemovesItFromFollowersFeeds()
    {
        var author = await api.RegisterAsync("author");
        var reposter = await api.RegisterAsync("reposter");
        var follower = await api.RegisterAsync("follower");
        await follower.FollowAsync(reposter);
        var post = await author.CreatePostAsync("x");
        await reposter.ToggleRetweetAsync(post.Id);
        Assert.Single(await follower.FeedAsync());

        await reposter.ToggleRetweetAsync(post.Id);

        Assert.Empty(await follower.FeedAsync());
    }

    [Fact]
    public async Task YourOwnRepost_AppearsInYourFeedAndOnYourProfile()
    {
        var author = await api.RegisterAsync("author");
        var me = await api.RegisterAsync("me");
        var post = await author.CreatePostAsync("interesting");   // I don't follow the author
        await me.ToggleRetweetAsync(post.Id);

        var feed = await me.FeedAsync();
        var inFeed = Assert.Single(feed);
        Assert.Equal(me.Id, inFeed.RetweetedBy!.Id);
        Assert.True(inFeed.IsRetweeted);

        var profile = await api.Anonymous.GetFromJsonAsync<List<PostResponse>>($"/api/posts/user/{me.Id}", TestUser.Json);
        var onProfile = Assert.Single(profile!);
        Assert.Equal(post.Id, onProfile.Id);
        Assert.Equal(me.Id, onProfile.RetweetedBy!.Id);
    }

    [Fact]
    public async Task ARepostOfAFollowedAuthorsPost_AppearsTwice_OncePerEntry()
    {
        var author = await api.RegisterAsync("author");
        var reposter = await api.RegisterAsync("reposter");
        var follower = await api.RegisterAsync("follower");
        await follower.FollowAsync(author);
        await follower.FollowAsync(reposter);
        var post = await author.CreatePostAsync("seen twice");
        await reposter.ToggleRetweetAsync(post.Id);

        var feed = await follower.FeedAsync();

        Assert.Equal(2, feed.Count);
        Assert.Equal(new[] { true, false }, feed.Select(e => e.RetweetedBy != null));   // the repost is newer, so it comes first
        Assert.All(feed, e => Assert.Equal(post.Id, e.Id));
    }

    [Fact]
    public async Task ARepliesCanBeRepostedToo()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("root");
        var reply = await bob.ReplyAsync(post.Id, "a reply worth sharing");
        var carol = await api.RegisterAsync("carol");
        await alice.FollowAsync(carol);

        await carol.ToggleRetweetAsync(reply.Id);

        var entry = Assert.Single(await alice.FeedAsync(), e => e.RetweetedBy != null);
        Assert.Equal(reply.Id, entry.Id);
        Assert.Equal(alice.Username, entry.ReplyToUsername);   // still shows what it replies to
    }

    [Fact]
    public async Task Reposting_RequiresLogin_AndAnExistingPost()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("x");

        await (await api.Anonymous.PostAsJsonAsync($"/api/retweets/toggle/{post.Id}", new { })).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await alice.PostAsync("/api/retweets/toggle/99999999")).ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletingAPost_RemovesItsRepostsEverywhere()
    {
        var author = await api.RegisterAsync("author");
        var reposter = await api.RegisterAsync("reposter");
        var follower = await api.RegisterAsync("follower");
        await follower.FollowAsync(reposter);
        var post = await author.CreatePostAsync("doomed");
        await reposter.ToggleRetweetAsync(post.Id);
        Assert.Single(await follower.FeedAsync());

        await (await author.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        Assert.Empty(await follower.FeedAsync());
        Assert.Empty(await api.Anonymous.GetFromJsonAsync<List<PostResponse>>($"/api/posts/user/{reposter.Id}", TestUser.Json) ?? []);
    }
}
