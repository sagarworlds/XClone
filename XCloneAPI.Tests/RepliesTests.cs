using System.Net;
using System.Net.Http.Json;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

[Collection(ApiCollection.Name)]
public class RepliesTests(ApiFixture api)
{
    [Fact]
    public async Task Reply_KnowsItsParentAndWhoItRepliesTo()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("original");

        var reply = await bob.ReplyAsync(post.Id, "a reply");

        Assert.Equal(post.Id, reply.ParentPostId);
        Assert.Equal(alice.Username, reply.ReplyToUsername);
        Assert.Equal(bob.Id, reply.UserId);
        Assert.Equal("a reply", reply.Content);

        var fetched = await bob.GetPostAsync(reply.Id);
        Assert.Equal(post.Id, fetched.ParentPostId);
        Assert.Equal(alice.Username, fetched.ReplyToUsername);
    }

    [Fact]
    public async Task RepliesCanBeNested_AndEachPostCountsOnlyItsDirectReplies()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var post = await alice.CreatePostAsync("root");

        var first = await bob.ReplyAsync(post.Id, "reply to root");
        var nested = await carol.ReplyAsync(first.Id, "reply to the reply");
        await carol.ReplyAsync(post.Id, "another reply to root");

        Assert.Equal(bob.Username, nested.ReplyToUsername);
        Assert.Equal(2, (await alice.GetPostAsync(post.Id)).RepliesCount);    // not 3: the nested one belongs to `first`
        Assert.Equal(1, (await alice.GetPostAsync(first.Id)).RepliesCount);
    }

    [Fact]
    public async Task Thread_ListsDirectRepliesOldestFirst_AndIsPublic()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("root");
        var first = await bob.ReplyAsync(post.Id, "first");
        var second = await alice.ReplyAsync(post.Id, "second");
        await bob.ReplyAsync(first.Id, "nested, must not appear at the top level");

        var thread = await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/{post.Id}/replies");

        Assert.Equal(new[] { first.Id, second.Id }, thread!.Select(r => r.Id));
        Assert.All(thread, r => Assert.Equal(alice.Username, r.ReplyToUsername));
    }

    [Fact]
    public async Task Thread_PagesAndBoundsItsSize()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("root");
        var ids = new List<int>();
        for (var i = 0; i < 3; i++)
            ids.Add((await alice.ReplyAsync(post.Id, $"reply {i}")).Id);

        var pages = await api.Anonymous.ReadAllPagesAsync<PostResponse>($"/api/posts/{post.Id}/replies", take: 2);
        Assert.Equal(new[] { 2, 1 }, pages.Select(p => p.Items.Count));
        Assert.Equal(ids, pages.SelectMany(p => p.Items).Select(r => r.Id));

        await (await api.Anonymous.GetAsync($"/api/posts/{post.Id}/replies?take=100000")).ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reply_IsValidatedLikeAPost()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("root");

        await (await alice.PostAsync("/api/posts/99999999/replies", new { content = "orphan" })).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await alice.PostAsync($"/api/posts/{post.Id}/replies", new { content = "   " })).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await alice.PostAsync($"/api/posts/{post.Id}/replies", new { content = new string('x', 281) })).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await api.Anonymous.PostAsJsonAsync($"/api/posts/{post.Id}/replies", new { content = "hi" })).ShouldBeAsync(HttpStatusCode.Unauthorized);

        Assert.Equal(0, (await alice.GetPostAsync(post.Id)).RepliesCount);   // failed replies didn't change the count
    }

    [Fact]
    public async Task Replies_AreKeptOutOfTheFeedAndTheProfilePostsTab_ButShowOnTheRepliesTab()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        await alice.FollowAsync(bob);
        var post = await alice.CreatePostAsync("root");
        var reply = await bob.ReplyAsync(post.Id, "bob replies");

        Assert.DoesNotContain(reply.Id, (await alice.FeedAsync()).Select(p => p.Id));   // alice follows bob, still not in her feed

        var bobsPosts = await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/user/{bob.Id}");
        Assert.Empty(bobsPosts!);

        var bobsReplies = await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/user/{bob.Id}/replies");
        var item = Assert.Single(bobsReplies!);
        Assert.Equal(reply.Id, item.Id);
        Assert.Equal(alice.Username, item.ReplyToUsername);

        // ...and the original post is not in anyone's Replies tab.
        Assert.Empty((await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/user/{alice.Id}/replies"))!);
    }

    [Fact]
    public async Task ARepliesLikeStateIsTrackedPerViewer()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("root");
        var reply = await bob.ReplyAsync(post.Id, "reply");
        await alice.ToggleLikeAsync(reply.Id);

        var asAlice = await alice.GetItemsAsync<PostResponse>($"/api/posts/{post.Id}/replies");
        var asBob = await bob.GetItemsAsync<PostResponse>($"/api/posts/{post.Id}/replies");

        Assert.True(Assert.Single(asAlice).IsLiked);
        Assert.False(Assert.Single(asBob).IsLiked);
        Assert.Equal(1, Assert.Single(asBob).LikesCount);
    }

    // ---- deleting -------------------------------------------------------------------------------------------

    [Fact]
    public async Task DeletingAReply_LowersTheParentsCount_AndRemovesItsOwnReplies()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var post = await alice.CreatePostAsync("root");
        var reply = await bob.ReplyAsync(post.Id, "will be deleted");
        var nested = await carol.ReplyAsync(reply.Id, "goes with it");
        Assert.Equal(1, (await alice.GetPostAsync(post.Id)).RepliesCount);

        await (await bob.DeleteAsync($"/api/posts/{reply.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        Assert.Equal(0, (await alice.GetPostAsync(post.Id)).RepliesCount);
        await (await alice.GetAsync($"/api/posts/{nested.Id}")).ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Empty(await alice.GetItemsAsync<PostResponse>($"/api/posts/{post.Id}/replies"));
    }

    [Fact]
    public async Task DeletingAPost_RemovesAllOfItsReplies()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("root");
        var reply = await bob.ReplyAsync(post.Id, "reply");

        await (await alice.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        await (await bob.GetAsync($"/api/posts/{reply.Id}")).ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Empty(await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/user/{bob.Id}/replies") ?? []);
    }

    [Fact]
    public async Task YouCannotDeleteSomeoneElsesReply_EvenOnYourOwnPost()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("root");
        var reply = await bob.ReplyAsync(post.Id, "bob's reply");

        await (await alice.DeleteAsync($"/api/posts/{reply.Id}")).ShouldBeAsync(HttpStatusCode.NotFound);

        Assert.Equal(1, (await alice.GetPostAsync(post.Id)).RepliesCount);
    }
}
