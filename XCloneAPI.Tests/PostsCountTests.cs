using System.Net;
using System.Net.Http.Json;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>The post count on a profile is what its Posts tab lists: top-level posts plus reposts, never replies.</summary>
[Collection(ApiCollection.Name)]
public class PostsCountTests(ApiFixture api)
{
    private static Task<UserResponse> ProfileOfAsync(TestUser viewer, TestUser who) =>
        viewer.GetAsync<UserResponse>($"/api/users/profile/{who.Username}");

    private async Task<int> CountAsync(TestUser who) => (await ProfileOfAsync(who, who)).PostsCount;

    [Fact]
    public async Task ANewUserHasNoPosts()
    {
        var alice = await api.RegisterAsync("alice");

        Assert.Equal(0, await CountAsync(alice));
    }

    [Fact]
    public async Task EveryPostCounts()
    {
        var alice = await api.RegisterAsync("alice");

        await alice.CreatePostAsync("one");
        await alice.CreatePostAsync("two");
        await alice.CreatePostAsync("three");

        Assert.Equal(3, await CountAsync(alice));
    }

    [Fact]
    public async Task RepliesDoNotCount_ForTheReplierOrForThePostRepliedTo()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("root");

        var reply = await bob.ReplyAsync(post.Id, "a reply");
        await alice.ReplyAsync(reply.Id, "a reply to the reply");

        Assert.Equal(1, await CountAsync(alice));   // just the root
        Assert.Equal(0, await CountAsync(bob));
    }

    [Fact]
    public async Task RepostsCount_ForWhoeverReposted_AndUndoingOneTakesItBack()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("worth sharing");

        await bob.ToggleRetweetAsync(post.Id);

        Assert.Equal(1, await CountAsync(bob));
        Assert.Equal(1, await CountAsync(alice));   // being reposted does not add to the author's count

        await bob.ToggleRetweetAsync(post.Id);

        Assert.Equal(0, await CountAsync(bob));
    }

    [Fact]
    public async Task RepostingYourOwnPost_ShowsItTwiceInYourPostsTab_SoItCountsTwice()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("mine");

        await alice.ToggleRetweetAsync(post.Id);

        Assert.Equal(2, await CountAsync(alice));
        Assert.Equal(2, (await alice.GetItemsAsync<PostResponse>($"/api/posts/user/{alice.Id}")).Count);
    }

    [Fact]
    public async Task RepostingAReplyCounts_BecauseItAppearsInThePostsTab()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("root");
        var reply = await bob.ReplyAsync(post.Id, "quotable");
        var carol = await api.RegisterAsync("carol");

        await carol.ToggleRetweetAsync(reply.Id);

        Assert.Equal(1, await CountAsync(carol));
        Assert.Equal(0, await CountAsync(bob));
    }

    [Fact]
    public async Task DeletingAPost_LowersTheCount_AndSoDoesRemovingItsRepostsFromOtherPeoplesProfiles()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var keep = await alice.CreatePostAsync("keep");
        var doomed = await alice.CreatePostAsync("doomed");
        await bob.ToggleRetweetAsync(doomed.Id);
        await bob.ToggleRetweetAsync(keep.Id);
        Assert.Equal(2, await CountAsync(alice));
        Assert.Equal(2, await CountAsync(bob));

        await (await alice.DeleteAsync($"/api/posts/{doomed.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        Assert.Equal(1, await CountAsync(alice));
        Assert.Equal(1, await CountAsync(bob));   // the repost of the deleted post is gone
    }

    [Fact]
    public async Task DeletingAReply_DoesNotChangeTheCount()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("root");
        var reply = await bob.ReplyAsync(post.Id, "regrets");

        await (await bob.DeleteAsync($"/api/posts/{reply.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        Assert.Equal(0, await CountAsync(bob));
        Assert.Equal(1, await CountAsync(alice));
    }

    [Fact]
    public async Task ItMatchesWhatThePostsTabActuallyLists()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var bobsPost = await bob.CreatePostAsync("bob's");
        var bobsReply = await alice.ReplyAsync(bobsPost.Id, "alice replies");
        for (var i = 1; i <= 4; i++)
            await alice.CreatePostAsync($"alice {i}");
        await alice.ToggleRetweetAsync(bobsPost.Id);
        await bob.ToggleRetweetAsync(bobsReply.Id);   // bob reposts alice's reply: not alice's entry

        var listed = await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/user/{alice.Id}?take=50");

        Assert.Equal(listed!.Count, await CountAsync(alice));
        Assert.Equal(5, listed.Count);
    }

    [Fact]
    public async Task OtherPeoplesActivity_DoesNotLeakIntoYourCount()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        await bob.CreatePostAsync("bob's post");
        await bob.CreatePostAsync("another");

        Assert.Equal(0, await CountAsync(alice));
        Assert.Equal(2, await CountAsync(bob));
    }

    // ---- where it shows up -----------------------------------------------------------------------------------

    [Fact]
    public async Task EveryProfileEndpointCarriesIt_IncludingForAnonymousVisitors()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        await bob.CreatePostAsync("one");
        await bob.CreatePostAsync("two");
        await alice.FollowAsync(bob);

        Assert.Equal(2, (await alice.GetAsync<UserResponse>($"/api/users/profile/{bob.Username}")).PostsCount);
        Assert.Equal(2, (await alice.GetAsync<UserResponse>($"/api/users/{bob.Id}")).PostsCount);
        Assert.Equal(2, (await api.Anonymous.GetFromJsonAsync<UserResponse>($"/api/users/{bob.Id}", TestUser.Json))!.PostsCount);
        Assert.Equal(2, (await bob.GetAsync<UserResponse>("/api/users/profile")).PostsCount);

        var found = await api.Anonymous.GetFromJsonAsync<List<UserResponse>>($"/api/users/search?query={bob.Username}", TestUser.Json);
        Assert.Equal(2, Assert.Single(found!, u => u.Id == bob.Id).PostsCount);

        var followers = await api.Anonymous.GetFromJsonAsync<List<UserResponse>>($"/api/users/{bob.Id}/followers", TestUser.Json);
        Assert.Equal(0, Assert.Single(followers!).PostsCount);   // alice, who has not posted
    }

    [Fact]
    public async Task UpdatingYourProfile_ReturnsTheCountToo()
    {
        var alice = await api.RegisterAsync("alice");
        await alice.CreatePostAsync("one");

        var response = await alice.PutAsync("/api/users/profile", new { displayName = "Renamed" });
        await response.ShouldBeAsync(HttpStatusCode.OK);

        Assert.Equal(1, (await response.ReadAsync<UserResponse>()).PostsCount);
    }
}
