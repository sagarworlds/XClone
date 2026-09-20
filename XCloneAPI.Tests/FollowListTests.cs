using System.Net;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>
/// The followers / following lists (GET /api/users/{id}/followers and /following): newest follow first, paged with
/// cursors, public, and safe to read while people follow and unfollow.
/// </summary>
[Collection(ApiCollection.Name)]
public class FollowListTests(ApiFixture api)
{
    private static string Followers(TestUser user) => $"/api/users/{user.Id}/followers";
    private static string Following(TestUser user) => $"/api/users/{user.Id}/following";

    /// <summary>Registers users who follow the target one after the other, so the oldest follower is first in the list.</summary>
    private async Task<List<TestUser>> FollowedBy(TestUser target, int count)
    {
        var fans = new List<TestUser>();
        for (var i = 1; i <= count; i++)
        {
            var fan = await api.RegisterAsync($"fan{i}");
            await fan.FollowAsync(target);
            fans.Add(fan);
        }

        return fans;   // oldest follow first
    }

    /// <summary>Registers users that the given one follows one after the other.</summary>
    private async Task<List<TestUser>> Follows(TestUser follower, int count)
    {
        var stars = new List<TestUser>();
        for (var i = 1; i <= count; i++)
        {
            var star = await api.RegisterAsync($"star{i}");
            await follower.FollowAsync(star);
            stars.Add(star);
        }

        return stars;
    }

    private static List<int> Ids(IEnumerable<UserResponse> users) => users.Select(u => u.Id).ToList();

    private static List<int> Ids(IEnumerable<TestUser> users) => users.Select(u => u.Id).ToList();

    // ---- order and direction --------------------------------------------------------------------------------

    [Fact]
    public async Task Followers_AreListedNewestFollowFirst()
    {
        var alice = await api.RegisterAsync("alice");
        var fans = await FollowedBy(alice, 3);

        var page = await api.Anonymous.GetPageAsync<UserResponse>(Followers(alice));

        Assert.Equal(Ids(fans.AsEnumerable().Reverse()), Ids(page.Items));
    }

    [Fact]
    public async Task Following_IsListedNewestFollowFirst()
    {
        var alice = await api.RegisterAsync("alice");
        var stars = await Follows(alice, 3);

        var page = await api.Anonymous.GetPageAsync<UserResponse>(Following(alice));

        Assert.Equal(Ids(stars.AsEnumerable().Reverse()), Ids(page.Items));
    }

    [Fact]
    public async Task EachListHoldsTheRightDirection_AndOnlyThatUser()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var dave = await api.RegisterAsync("dave");
        await bob.FollowAsync(alice);     // bob follows alice
        await alice.FollowAsync(carol);   // alice follows carol
        await dave.FollowAsync(carol);    // unrelated to alice

        var followers = await api.Anonymous.GetItemsAsync<UserResponse>(Followers(alice));
        var following = await api.Anonymous.GetItemsAsync<UserResponse>(Following(alice));

        Assert.Equal(new[] { bob.Id }, Ids(followers));
        Assert.Equal(new[] { carol.Id }, Ids(following));
    }

    [Fact]
    public async Task ANewcomerHasEmptyLists_WithNoCursor()
    {
        var newcomer = await api.RegisterAsync("newcomer");

        foreach (var path in new[] { Followers(newcomer), Following(newcomer) })
        {
            var page = await api.Anonymous.GetPageAsync<UserResponse>(path);
            Assert.Empty(page.Items);
            Assert.Null(page.NextCursor);
        }
    }

    [Fact]
    public async Task AUserWhoDoesNotExist_HasEmptyLists_NotAnError()
    {
        foreach (var path in new[] { "/api/users/99999999/followers", "/api/users/99999999/following" })
        {
            var page = await api.Anonymous.GetPageAsync<UserResponse>(path);
            Assert.Empty(page.Items);
            Assert.Null(page.NextCursor);
        }
    }

    // ---- paging ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Pages_CoverEveryFollowerExactlyOnce_AndTheLastHasNoCursor()
    {
        var alice = await api.RegisterAsync("alice");
        var fans = await FollowedBy(alice, 5);

        var pages = await api.Anonymous.ReadAllPagesAsync<UserResponse>(Followers(alice), take: 2);

        Assert.Equal(new[] { 2, 2, 1 }, pages.Select(p => p.Items.Count));
        Assert.All(pages.SkipLast(1), p => Assert.NotNull(p.NextCursor));
        Assert.Null(pages[^1].NextCursor);
        Assert.Equal(Ids(fans.AsEnumerable().Reverse()), Ids(pages.SelectMany(p => p.Items)));
    }

    [Fact]
    public async Task AnExactMultipleOfThePageSize_NeedsNoEmptyExtraRequest()
    {
        var alice = await api.RegisterAsync("alice");
        await Follows(alice, 4);

        var pages = await api.Anonymous.ReadAllPagesAsync<UserResponse>(Following(alice), take: 2);

        Assert.Equal(new[] { 2, 2 }, pages.Select(p => p.Items.Count));
        Assert.Null(pages[^1].NextCursor);
    }

    [Fact]
    public async Task ANewFollowerAtTheTop_DoesNotShiftTheNextPage()
    {
        var alice = await api.RegisterAsync("alice");
        var fans = await FollowedBy(alice, 4);
        var first = await api.Anonymous.GetPageAsync<UserResponse>(Followers(alice), take: 2);

        var latecomer = await api.RegisterAsync("latecomer");
        await latecomer.FollowAsync(alice);
        var second = await api.Anonymous.GetPageAsync<UserResponse>(Followers(alice), first.NextCursor, take: 2);

        Assert.Equal(new[] { fans[3].Id, fans[2].Id }, Ids(first.Items));
        Assert.Equal(new[] { fans[1].Id, fans[0].Id }, Ids(second.Items));   // nothing repeated, nothing skipped
    }

    [Fact]
    public async Task UnfollowingWhatYouHaveSeen_EvenTheEntryTheCursorPointsAt_SkipsNothing()
    {
        var alice = await api.RegisterAsync("alice");
        var fans = await FollowedBy(alice, 5);
        var first = await api.Anonymous.GetPageAsync<UserResponse>(Followers(alice), take: 2);   // fans 5 and 4

        await fans[3].PostAsync($"/api/follows/toggle/{alice.Id}");   // fan 4, the last one seen, unfollows
        await fans[4].PostAsync($"/api/follows/toggle/{alice.Id}");   // and so does fan 5
        var rest = await api.Anonymous.ReadAllPagesAsync<UserResponse>(Followers(alice), take: 2);
        var second = await api.Anonymous.GetPageAsync<UserResponse>(Followers(alice), first.NextCursor, take: 50);

        Assert.Equal(new[] { fans[2].Id, fans[1].Id, fans[0].Id }, Ids(second.Items));
        Assert.Equal(new[] { fans[2].Id, fans[1].Id, fans[0].Id }, Ids(rest.SelectMany(p => p.Items)));
    }

    [Fact]
    public async Task UnfollowingWhatIsStillAhead_JustLeavesItOut()
    {
        var alice = await api.RegisterAsync("alice");
        var fans = await FollowedBy(alice, 5);
        var first = await api.Anonymous.GetPageAsync<UserResponse>(Followers(alice), take: 2);   // fans 5 and 4

        await fans[1].PostAsync($"/api/follows/toggle/{alice.Id}");   // fan 2 is still ahead
        var second = await api.Anonymous.GetPageAsync<UserResponse>(Followers(alice), first.NextCursor, take: 50);

        Assert.Equal(new[] { fans[2].Id, fans[0].Id }, Ids(second.Items));
    }

    [Fact]
    public async Task AFollowingListPagesTheSameWay()
    {
        var alice = await api.RegisterAsync("alice");
        var stars = await Follows(alice, 4);
        var first = await api.Anonymous.GetPageAsync<UserResponse>(Following(alice), take: 2);

        await alice.PostAsync($"/api/follows/toggle/{(await api.RegisterAsync("late")).Id}");   // follows someone new
        var second = await api.Anonymous.GetPageAsync<UserResponse>(Following(alice), first.NextCursor, take: 2);

        Assert.Equal(new[] { stars[3].Id, stars[2].Id }, Ids(first.Items));
        Assert.Equal(new[] { stars[1].Id, stars[0].Id }, Ids(second.Items));
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task TheOldSkipParameterIsGone_ItDoesNothing()
    {
        var alice = await api.RegisterAsync("alice");
        await FollowedBy(alice, 4);

        var skipped = await api.Anonymous.GetPageAsync<UserResponse>($"{Followers(alice)}?skip=3", take: 50);

        Assert.Equal(4, skipped.Items.Count);
    }

    [Fact]
    public async Task ThePageSize_IsClamped_NotRejected()
    {
        var alice = await api.RegisterAsync("alice");
        await FollowedBy(alice, 3);

        Assert.Single((await api.Anonymous.GetPageAsync<UserResponse>(Followers(alice), take: 0)).Items);
        Assert.Single((await api.Anonymous.GetPageAsync<UserResponse>(Followers(alice), take: -5)).Items);
        Assert.Equal(3, (await api.Anonymous.GetPageAsync<UserResponse>(Followers(alice), take: 100000)).Items.Count);
    }

    // ---- what a row says -------------------------------------------------------------------------------------

    [Fact]
    public async Task IsFollowed_IsRelativeToWhoIsAsking()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        await bob.FollowAsync(alice);
        await carol.FollowAsync(alice);
        await bob.FollowAsync(carol);   // bob follows carol, but not himself, and nobody follows bob

        var seenByBob = await bob.GetItemsAsync<UserResponse>(Followers(alice));
        var seenByAnonymous = await api.Anonymous.GetItemsAsync<UserResponse>(Followers(alice));

        Assert.True(seenByBob.Single(u => u.Id == carol.Id).IsFollowed);
        Assert.False(seenByBob.Single(u => u.Id == bob.Id).IsFollowed);   // you do not follow yourself
        Assert.All(seenByAnonymous, u => Assert.False(u.IsFollowed));
    }

    [Fact]
    public async Task EachRowCarriesItsOwnCounts_AndNoEmail()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        await bob.FollowAsync(alice);
        await carol.FollowAsync(bob);   // bob has a follower of his own
        await bob.CreatePostAsync("bob's first");
        await bob.CreatePostAsync("bob's second");

        var row = Assert.Single(await api.Anonymous.GetItemsAsync<UserResponse>(Followers(alice)));

        Assert.Equal(bob.Id, row.Id);
        Assert.Equal(bob.Username, row.Username);
        Assert.Equal(1, row.FollowersCount);
        Assert.Equal(1, row.FollowingCount);
        Assert.Equal(2, row.PostsCount);
        Assert.True(string.IsNullOrEmpty(row.Email));
    }

    [Fact]
    public async Task ABrokenCursor_IsA400_OnBothLists()
    {
        var alice = await api.RegisterAsync("alice");

        foreach (var path in new[] { Followers(alice), Following(alice) })
        {
            var response = await api.Anonymous.GetAsync(PagingExtensions.PagePath(path, "garbage"));

            await response.ShouldBeAsync(HttpStatusCode.BadRequest);
            Assert.Equal("Invalid cursor", await response.ReadMessageAsync());
        }
    }
}
