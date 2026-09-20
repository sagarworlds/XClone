using System.Net;
using System.Net.Http.Json;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

[Collection(ApiCollection.Name)]
public class LikesTests(ApiFixture api)
{
    [Fact]
    public async Task Toggle_LikesThenUnlikes_AndKeepsTheCountAndFlagInSync()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("like me");

        Assert.True(await bob.ToggleLikeAsync(post.Id));
        var liked = await bob.GetPostAsync(post.Id);
        Assert.Equal(1, liked.LikesCount);
        Assert.True(liked.IsLiked);
        Assert.False((await alice.GetPostAsync(post.Id)).IsLiked);   // the flag is per viewer

        Assert.False(await bob.ToggleLikeAsync(post.Id));
        var unliked = await bob.GetPostAsync(post.Id);
        Assert.Equal(0, unliked.LikesCount);
        Assert.False(unliked.IsLiked);
    }

    [Fact]
    public async Task SeveralPeopleCanLikeTheSamePost()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var post = await alice.CreatePostAsync("popular");

        await bob.ToggleLikeAsync(post.Id);
        await carol.ToggleLikeAsync(post.Id);

        Assert.Equal(2, (await alice.GetPostAsync(post.Id)).LikesCount);
    }

    [Fact]
    public async Task LikeFlag_ShowsUpInTheFeed()
    {
        var me = await api.RegisterAsync("me");
        var liked = await me.CreatePostAsync("liked");
        var notLiked = await me.CreatePostAsync("not liked");
        await me.ToggleLikeAsync(liked.Id);

        var feed = await me.FeedAsync();

        Assert.True(feed.Single(p => p.Id == liked.Id).IsLiked);
        Assert.False(feed.Single(p => p.Id == notLiked.Id).IsLiked);
    }

    [Fact]
    public async Task Liking_RequiresLogin_AndAnExistingPost()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("x");

        await (await api.Anonymous.PostAsJsonAsync($"/api/likes/toggle/{post.Id}", new { })).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await alice.PostAsync("/api/likes/toggle/99999999")).ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LikersList_IsPublic_ListsWhoLiked_AndHidesEmails()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("x");
        await bob.ToggleLikeAsync(post.Id);

        var likers = await api.Anonymous.GetFromJsonAsync<List<UserResponse>>($"/api/likes/post/{post.Id}", TestUser.Json);

        var liker = Assert.Single(likers!);
        Assert.Equal(bob.Id, liker.Id);
        Assert.True(string.IsNullOrEmpty(liker.Email));
    }

    [Fact]
    public async Task LikersList_ForAPostThatDoesNotExist_Is404NotAServerError()
    {
        await (await api.Anonymous.GetAsync("/api/likes/post/99999999")).ShouldBeAsync(HttpStatusCode.NotFound);
    }
}
