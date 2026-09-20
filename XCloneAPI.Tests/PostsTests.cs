using System.Net;
using System.Net.Http.Json;
using XCloneAPI.DTOs;
using XCloneAPI.Models;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

[Collection(ApiCollection.Name)]
public class PostsTests(ApiFixture api)
{
    // ---- creating -------------------------------------------------------------------------------------------

    [Fact]
    public async Task CreatePost_ReturnsTheNewPostWithTheAuthor()
    {
        var alice = await api.RegisterAsync("alice");

        var post = await alice.CreatePostAsync("hello world");

        Assert.Equal("hello world", post.Content);
        Assert.Equal(alice.Id, post.UserId);
        Assert.Equal(alice.Username, post.User.Username);
        Assert.Equal(0, post.LikesCount);
        Assert.Equal(0, post.RepliesCount);
        Assert.Equal(0, post.RetweetsCount);
        Assert.Null(post.ParentPostId);
        Assert.False(post.IsLiked);
        Assert.False(post.IsRetweeted);
        Assert.Null(post.RetweetedBy);
        Assert.True(post.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task CreatePost_AllowsExactly280Characters_ButNotEmptyOrLonger()
    {
        var alice = await api.RegisterAsync("alice");

        await (await alice.PostAsync("/api/posts", new { content = new string('a', 280) })).ShouldBeAsync(HttpStatusCode.Created);
        await (await alice.PostAsync("/api/posts", new { content = new string('a', 281) })).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await alice.PostAsync("/api/posts", new { content = "" })).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await alice.PostAsync("/api/posts", new { content = "    " })).ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePost_OnlyAcceptsHttpUrlsAsMedia()
    {
        var alice = await api.RegisterAsync("alice");

        await (await alice.PostAsync("/api/posts", new { content = "x", mediaUrls = new[] { "javascript:alert(1)" } })).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await alice.PostAsync("/api/posts", new { content = "x", mediaUrls = new[] { "https://example.com/ok.png", "nope" } })).ShouldBeAsync(HttpStatusCode.BadRequest);

        var ok = await alice.PostAsync("/api/posts", new { content = "with a picture", mediaUrls = new[] { "https://example.com/ok.png" } });
        await ok.ShouldBeAsync(HttpStatusCode.Created);
        Assert.Equal(new[] { "https://example.com/ok.png" }, (await ok.ReadAsync<PostResponse>()).MediaUrls);

        // Media is optional.
        var plain = await alice.CreatePostAsync("no media");
        Assert.Empty(plain.MediaUrls);
    }

    [Fact]
    public async Task CreatePost_RequiresLogin()
    {
        await (await api.Anonymous.PostAsJsonAsync("/api/posts", new { content = "hi" })).ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    // ---- reading --------------------------------------------------------------------------------------------

    [Fact]
    public async Task GetPost_IsPublic_AndNeverExposesTheAuthorsEmail()
    {
        var alice = await api.RegisterAsync("alice");
        var created = await alice.CreatePostAsync("public post");

        var post = await api.Anonymous.GetFromJsonAsync<PostResponse>($"/api/posts/{created.Id}", TestUser.Json);

        Assert.Equal("public post", post!.Content);
        Assert.False(post.IsLiked);
        Assert.True(string.IsNullOrEmpty(post.User.Email));
        await (await api.Anonymous.GetAsync("/api/posts/99999999")).ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UserPosts_ListsOnlyThatUsersPosts_NewestFirst()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var first = await alice.CreatePostAsync("first");
        await bob.CreatePostAsync("bob's post");
        var second = await alice.CreatePostAsync("second");

        var posts = await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/user/{alice.Id}");

        Assert.Equal(new[] { second.Id, first.Id }, posts!.Select(p => p.Id));
    }

    // ---- deleting -------------------------------------------------------------------------------------------

    [Fact]
    public async Task DeletePost_RemovesItForGood()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("short lived");

        await (await alice.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        await (await alice.GetAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await alice.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.DoesNotContain(post.Id, (await alice.FeedAsync()).Select(p => p.Id));
    }

    [Fact]
    public async Task DeletePost_OnlyTheAuthorCanDoIt()
    {
        var alice = await api.RegisterAsync("alice");
        var mallory = await api.RegisterAsync("mallory");
        var post = await alice.CreatePostAsync("mine");

        await (await mallory.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await api.Anonymous.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.Unauthorized);

        Assert.Equal("mine", (await alice.GetPostAsync(post.Id)).Content);   // still there
        await (await alice.DeleteAsync("/api/posts/99999999")).ShouldBeAsync(HttpStatusCode.NotFound);
    }

    // ---- home feed ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Feed_ContainsYourPostsAndPostsFromPeopleYouFollow_NewestFirst()
    {
        var me = await api.RegisterAsync("me");
        var followed = await api.RegisterAsync("followed");
        var stranger = await api.RegisterAsync("stranger");
        await me.FollowAsync(followed);

        var strangers = await stranger.CreatePostAsync("from a stranger");
        var a = await followed.CreatePostAsync("from someone I follow");
        var b = await me.CreatePostAsync("my own post");

        var feed = await me.FeedAsync();

        Assert.Equal(new[] { b.Id, a.Id }, feed.Select(p => p.Id));
        Assert.DoesNotContain(strangers.Id, feed.Select(p => p.Id));
    }

    [Fact]
    public async Task Feed_IsEmptyForANewUser_AndRequiresLogin()
    {
        var newcomer = await api.RegisterAsync("newcomer");

        Assert.Empty(await newcomer.FeedAsync());
        await (await api.Anonymous.GetAsync("/api/posts/feed")).ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Feed_PagesCleanly_WithoutRepeatsOrGaps()
    {
        var me = await api.RegisterAsync("me");
        var ids = new List<int>();
        for (var i = 0; i < 5; i++)
            ids.Add((await me.CreatePostAsync($"post {i}")).Id);
        ids.Reverse();   // newest first

        var pages = await me.ReadAllPagesAsync<PostResponse>("/api/posts/feed", take: 2);

        Assert.Equal(ids, pages.SelectMany(p => p.Items).Select(p => p.Id));
        Assert.Equal(new[] { 2, 2, 1 }, pages.Select(p => p.Items.Count));
    }

    [Fact]
    public async Task Feed_PageSizeIsBounded_AndOddValuesDoNotBreakIt()
    {
        var me = await api.RegisterAsync("me");
        await me.CreatePostAsync("one");
        await me.CreatePostAsync("two");

        // 60 more posts inserted directly, so we can check the upper bound without 60 HTTP calls.
        await api.WithDbAsync(async db =>
        {
            for (var i = 0; i < 60; i++)
                db.Posts.Add(new Post { UserId = me.Id, Content = $"bulk {i}", MediaUrls = Array.Empty<string>() });
            await db.SaveChangesAsync();
        });

        Assert.Equal(50, (await me.FeedAsync(100000)).Count);
        Assert.Single(await me.FeedAsync(0));       // take is clamped up to 1
        Assert.Single(await me.FeedAsync(-3));

        var profilePosts = await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/user/{me.Id}?take=999999");
        Assert.Equal(50, profilePosts!.Count);
    }
}
