using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>The home feed merges original posts and reposts into one stream; these tests pin down its ordering and paging.</summary>
[Collection(ApiCollection.Name)]
public class TimelineTests(ApiFixture api)
{
    [Fact]
    public async Task Entries_AreOrderedByWhenTheyHappened_NotByWhenThePostWasWritten()
    {
        var author = await api.RegisterAsync("author");
        var reposter = await api.RegisterAsync("reposter");
        var me = await api.RegisterAsync("me");
        await me.FollowAsync(author);
        await me.FollowAsync(reposter);

        var q1 = await author.CreatePostAsync("q1");
        var q2 = await author.CreatePostAsync("q2");
        await reposter.ToggleRetweetAsync(q1.Id);    // q1 is reposted AFTER q2 was written...
        var q3 = await author.CreatePostAsync("q3");

        var order = (await me.FeedAsync()).Select(e => (e.Id, Reposted: e.RetweetedBy != null)).ToList();

        // ...so the repost of q1 sits between q3 and q2.
        Assert.Equal(new[] { (q3.Id, false), (q1.Id, true), (q2.Id, false), (q1.Id, false) }, order);
    }

    [Fact]
    public async Task Paging_ThroughTheMergedStream_NeverRepeatsOrSkipsAnEntry()
    {
        var author = await api.RegisterAsync("author");
        var reposter = await api.RegisterAsync("reposter");
        var me = await api.RegisterAsync("me");
        await me.FollowAsync(author);
        await me.FollowAsync(reposter);

        var posts = new List<int>();
        for (var i = 0; i < 4; i++)
            posts.Add((await author.CreatePostAsync($"post {i}")).Id);
        await reposter.ToggleRetweetAsync(posts[0]);
        await reposter.ToggleRetweetAsync(posts[2]);

        var whole = (await me.FeedAsync(0, 50)).Select(e => (e.Id, e.RetweetedBy?.Id)).ToList();
        Assert.Equal(6, whole.Count);   // 4 originals + 2 reposts

        foreach (var pageSize in new[] { 1, 2, 4 })
        {
            var paged = new List<(int, int?)>();
            for (var skip = 0; skip < whole.Count; skip += pageSize)
                paged.AddRange((await me.FeedAsync(skip, pageSize)).Select(e => (e.Id, e.RetweetedBy?.Id)));

            Assert.Equal(whole, paged);
        }
    }

    [Fact]
    public async Task OnlyRepostsFromPeopleYouFollowAppear()
    {
        var author = await api.RegisterAsync("author");
        var followedReposter = await api.RegisterAsync("followed");
        var strangerReposter = await api.RegisterAsync("stranger");
        var me = await api.RegisterAsync("me");
        await me.FollowAsync(followedReposter);
        var post = await author.CreatePostAsync("only reposted");

        await strangerReposter.ToggleRetweetAsync(post.Id);
        Assert.Empty(await me.FeedAsync());

        await followedReposter.ToggleRetweetAsync(post.Id);
        var entry = Assert.Single(await me.FeedAsync());
        Assert.Equal(followedReposter.Id, entry.RetweetedBy!.Id);
    }

    [Fact]
    public async Task EveryEntryCarriesTheViewersOwnLikeAndRepostFlags()
    {
        var author = await api.RegisterAsync("author");
        var reposter = await api.RegisterAsync("reposter");
        var me = await api.RegisterAsync("me");
        await me.FollowAsync(author);
        await me.FollowAsync(reposter);
        var liked = await author.CreatePostAsync("i like this");
        var plain = await author.CreatePostAsync("nothing special");
        await reposter.ToggleRetweetAsync(liked.Id);
        await me.ToggleLikeAsync(liked.Id);

        var feed = await me.FeedAsync();

        // The liked post appears twice (original + repost) and is liked in both entries.
        Assert.All(feed.Where(e => e.Id == liked.Id), e => Assert.True(e.IsLiked));
        Assert.Equal(2, feed.Count(e => e.Id == liked.Id));
        Assert.False(feed.Single(e => e.Id == plain.Id).IsLiked);
        Assert.All(feed, e => Assert.False(e.IsRetweeted));   // I haven't reposted anything
    }

    [Fact]
    public async Task Counters_AreConsistentAcrossEntriesOfTheSamePost()
    {
        var author = await api.RegisterAsync("author");
        var r1 = await api.RegisterAsync("r1");
        var r2 = await api.RegisterAsync("r2");
        var me = await api.RegisterAsync("me");
        await me.FollowAsync(author);
        await me.FollowAsync(r1);
        var post = await author.CreatePostAsync("counted");
        await r1.ToggleRetweetAsync(post.Id);
        await r2.ToggleRetweetAsync(post.Id);
        await r2.ReplyAsync(post.Id, "a reply");

        var entries = (await me.FeedAsync()).Where(e => e.Id == post.Id).ToList();

        Assert.Equal(2, entries.Count);
        Assert.All(entries, e =>
        {
            Assert.Equal(2, e.RetweetsCount);
            Assert.Equal(1, e.RepliesCount);
        });
    }
}
