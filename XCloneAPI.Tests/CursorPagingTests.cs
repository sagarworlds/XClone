using System.Net;
using System.Text;
using XCloneAPI.DTOs;
using XCloneAPI.Models;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>
/// Lists are read with cursors: a page starts right after the last entry the client saw. These tests pin down what that
/// buys over skip/take (nothing repeats or gets skipped when the list changes meanwhile) and how bad input is handled.
/// </summary>
[Collection(ApiCollection.Name)]
public class CursorPagingTests(ApiFixture api)
{
    private const string Feed = "/api/posts/feed";

    private static string Base64Url(string text) =>
        Convert.ToBase64String(Encoding.ASCII.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task<List<int>> PostIdsAsync(TestUser user, int count)
    {
        var ids = new List<int>();
        for (var i = 1; i <= count; i++)
            ids.Add((await user.CreatePostAsync($"post {i}")).Id);
        return ids;   // oldest first
    }

    // ---- the shape of a page ----------------------------------------------------------------------------------

    [Fact]
    public async Task TheLastPage_HasNoCursor_SoAnExactMultipleNeedsNoEmptyExtraRequest()
    {
        var me = await api.RegisterAsync("me");
        await PostIdsAsync(me, 4);

        var pages = await me.ReadAllPagesAsync<PostResponse>(Feed, take: 2);

        Assert.Equal(new[] { 2, 2 }, pages.Select(p => p.Items.Count));
        Assert.NotNull(pages[0].NextCursor);
        Assert.Null(pages[1].NextCursor);
    }

    [Fact]
    public async Task AListThatFitsOnOnePage_HasNoCursor()
    {
        var me = await api.RegisterAsync("me");
        await PostIdsAsync(me, 3);

        var page = await me.FeedPageAsync(take: 50);

        Assert.Equal(3, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task AnEmptyList_IsOneEmptyPageWithNoCursor()
    {
        var newcomer = await api.RegisterAsync("newcomer");

        var page = await newcomer.FeedPageAsync();

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task AnEmptyCursorMeansTheFirstPage()
    {
        var me = await api.RegisterAsync("me");
        await PostIdsAsync(me, 3);

        var withEmptyCursor = await me.GetAsync<PagedResponse<PostResponse>>($"{Feed}?cursor=&take=2");

        Assert.Equal(2, withEmptyCursor.Items.Count);
    }

    [Fact]
    public async Task TheOldSkipParameterIsGone_ItDoesNothing()
    {
        var me = await api.RegisterAsync("me");
        await PostIdsAsync(me, 4);

        var skipped = await me.GetAsync<PagedResponse<PostResponse>>($"{Feed}?skip=3&take=50");

        Assert.Equal(4, skipped.Items.Count);
    }

    // ---- what cursors are for: the list changes while you read it --------------------------------------------

    [Fact]
    public async Task ANewPostAtTheTop_DoesNotShiftTheNextPage()
    {
        var me = await api.RegisterAsync("me");
        var ids = await PostIdsAsync(me, 5);                       // 1..5, so the feed reads 5,4,3,2,1
        var first = await me.FeedPageAsync(take: 2);
        Assert.Equal(new[] { ids[4], ids[3] }, first.Items.Select(p => p.Id));

        await me.CreatePostAsync("written while reading");         // skip/take would now repeat post 4 on the next page

        var rest = new List<int>();
        for (var cursor = first.NextCursor; cursor != null;)
        {
            var page = await me.FeedPageAsync(cursor, 2);
            rest.AddRange(page.Items.Select(p => p.Id));
            cursor = page.NextCursor;
        }
        Assert.Equal(new[] { ids[2], ids[1], ids[0] }, rest);
    }

    [Fact]
    public async Task DeletingWhatYouHaveSeen_EvenTheEntryTheCursorPointsAt_SkipsNothing()
    {
        var me = await api.RegisterAsync("me");
        var ids = await PostIdsAsync(me, 5);
        var first = await me.FeedPageAsync(take: 2);               // 5,4 - the cursor points at 4

        await (await me.DeleteAsync($"/api/posts/{ids[3]}")).ShouldBeAsync(HttpStatusCode.NoContent);
        await (await me.DeleteAsync($"/api/posts/{ids[4]}")).ShouldBeAsync(HttpStatusCode.NoContent);

        var second = await me.FeedPageAsync(first.NextCursor, 2);  // skip/take would jump over posts 3 and 2 here
        Assert.Equal(new[] { ids[2], ids[1] }, second.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task DeletingWhatIsStillAhead_JustLeavesItOut()
    {
        var me = await api.RegisterAsync("me");
        var ids = await PostIdsAsync(me, 5);
        var first = await me.FeedPageAsync(take: 2);

        await (await me.DeleteAsync($"/api/posts/{ids[2]}")).ShouldBeAsync(HttpStatusCode.NoContent);

        var second = await me.FeedPageAsync(first.NextCursor, 2);
        Assert.Equal(new[] { ids[1], ids[0] }, second.Items.Select(p => p.Id));
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task OtherPeoplePostingAndDeleting_DoesNotDisturbAFeedYouAreReading()
    {
        var author = await api.RegisterAsync("author");
        var me = await api.RegisterAsync("me");
        await me.FollowAsync(author);
        var ids = await PostIdsAsync(author, 6);
        var first = await me.FeedPageAsync(take: 3);               // 6,5,4

        await PostIdsAsync(author, 3);                             // three new posts at the top
        await (await author.DeleteAsync($"/api/posts/{ids[5]}")).ShouldBeAsync(HttpStatusCode.NoContent);   // one you have seen goes away
        await (await author.DeleteAsync($"/api/posts/{ids[0]}")).ShouldBeAsync(HttpStatusCode.NoContent);   // one you have not seen too

        var second = await me.FeedPageAsync(first.NextCursor, 3);
        Assert.Equal(new[] { ids[2], ids[1] }, second.Items.Select(p => p.Id));
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task ARepostUndoneBetweenPages_LeavesTheRestOfTheFeedInPlace()
    {
        var author = await api.RegisterAsync("author");
        var reposter = await api.RegisterAsync("reposter");
        var me = await api.RegisterAsync("me");
        await me.FollowAsync(author);
        await me.FollowAsync(reposter);
        var ids = await PostIdsAsync(author, 4);
        await reposter.ToggleRetweetAsync(ids[0]);                 // the newest entry of all
        var first = await me.FeedPageAsync(take: 2);
        Assert.NotNull(first.Items[0].RetweetedBy);

        await reposter.ToggleRetweetAsync(ids[0]);                 // undone

        var second = await me.FeedPageAsync(first.NextCursor, 50);
        Assert.Equal(new[] { ids[2], ids[1], ids[0] }, second.Items.Select(p => p.Id));   // post 4 was on page one, so 3,2,1 are left
    }

    // ---- entries that share a moment -------------------------------------------------------------------------

    [Fact]
    public async Task EntriesThatShareTheSameMomentAndPost_AreStillPagedExactlyOnceEach()
    {
        var author = await api.RegisterAsync("author");
        var r1 = await api.RegisterAsync("r1");
        var r2 = await api.RegisterAsync("r2");
        var me = await api.RegisterAsync("me");
        await me.FollowAsync(author);
        await me.FollowAsync(r1);
        await me.FollowAsync(r2);

        // Everything happens at the very same moment (Postgres keeps microseconds, so that is the resolution used)
        var moment = new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
        var postIds = new List<int>();
        await api.WithDbAsync(async db =>
        {
            for (var i = 0; i < 3; i++)
                db.Posts.Add(new Post { UserId = author.Id, Content = $"tie {i}", MediaUrls = Array.Empty<string>(), CreatedAt = moment, UpdatedAt = moment });
            await db.SaveChangesAsync();
            postIds.AddRange(db.Posts.Where(p => p.UserId == author.Id).OrderBy(p => p.Id).Select(p => p.Id));

            // Two people repost the middle post in that same moment as well
            db.Retweets.Add(new Retweet { UserId = r1.Id, PostId = postIds[1], CreatedAt = moment });
            db.Retweets.Add(new Retweet { UserId = r2.Id, PostId = postIds[1], CreatedAt = moment });
            await db.SaveChangesAsync();
        });

        // Order inside a moment: higher post id first, and for the same post the reposts (higher user id first) before the original
        var expected = new List<(int Post, int? Reposter)>
        {
            (postIds[2], null),
            (postIds[1], Math.Max(r1.Id, r2.Id)),
            (postIds[1], Math.Min(r1.Id, r2.Id)),
            (postIds[1], null),
            (postIds[0], null),
        };

        foreach (var take in new[] { 1, 2, 3, 50 })
        {
            var pages = await me.ReadAllPagesAsync<PostResponse>(Feed, take);
            var seen = pages.SelectMany(p => p.Items).Select(e => (e.Id, e.RetweetedBy?.Id)).ToList();

            Assert.Equal(expected, seen);   // nothing repeated, nothing missing, always the same order
        }
    }

    [Fact]
    public async Task ManyPostsCreatedInARush_PageWithoutRepeatsOrGaps()
    {
        var me = await api.RegisterAsync("me");
        var ids = await PostIdsAsync(me, 25);
        ids.Reverse();

        foreach (var take in new[] { 1, 4, 7, 10 })
        {
            var pages = await me.ReadAllPagesAsync<PostResponse>(Feed, take);

            Assert.Equal(ids, pages.SelectMany(p => p.Items).Select(p => p.Id));
            Assert.All(pages.SkipLast(1), p => Assert.Equal(take, p.Items.Count));
        }
    }

    // ---- the other lists -------------------------------------------------------------------------------------

    [Fact]
    public async Task AProfilesPosts_PageWithCursorsToo_ForAnonymousVisitors()
    {
        var alice = await api.RegisterAsync("alice");
        var ids = await PostIdsAsync(alice, 5);
        ids.Reverse();

        var pages = await api.Anonymous.ReadAllPagesAsync<PostResponse>($"/api/posts/user/{alice.Id}", take: 2);

        Assert.Equal(ids, pages.SelectMany(p => p.Items).Select(p => p.Id));
        Assert.Equal(3, pages.Count);
    }

    [Fact]
    public async Task ThreadReplies_ReadOldestFirst_NewOnesArriveAtTheEnd_AndDeletionsSkipNothing()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("root");
        var replies = new List<int>();
        for (var i = 1; i <= 5; i++)
            replies.Add((await bob.ReplyAsync(post.Id, $"reply {i}")).Id);
        var path = $"/api/posts/{post.Id}/replies";
        var first = await api.Anonymous.GetPageAsync<PostResponse>(path, take: 2);
        Assert.Equal(new[] { replies[0], replies[1] }, first.Items.Select(r => r.Id));

        var late = await bob.ReplyAsync(post.Id, "reply 6");                                              // arrives after
        await (await bob.DeleteAsync($"/api/posts/{replies[1]}")).ShouldBeAsync(HttpStatusCode.NoContent);   // one already read goes away
        await (await bob.DeleteAsync($"/api/posts/{replies[2]}")).ShouldBeAsync(HttpStatusCode.NoContent);   // one still to come goes away

        var second = await api.Anonymous.GetPageAsync<PostResponse>(path, first.NextCursor, 2);
        Assert.Equal(new[] { replies[3], replies[4] }, second.Items.Select(r => r.Id));
        var third = await api.Anonymous.GetPageAsync<PostResponse>(path, second.NextCursor, 2);
        Assert.Equal(new[] { late.Id }, third.Items.Select(r => r.Id));
        Assert.Null(third.NextCursor);
    }

    [Fact]
    public async Task AProfilesReplies_ReadNewestFirst()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("root");
        var replies = new List<int>();
        for (var i = 1; i <= 5; i++)
            replies.Add((await bob.ReplyAsync(post.Id, $"reply {i}")).Id);
        replies.Reverse();

        var pages = await api.Anonymous.ReadAllPagesAsync<PostResponse>($"/api/posts/user/{bob.Id}/replies", take: 2);

        Assert.Equal(new[] { 2, 2, 1 }, pages.Select(p => p.Items.Count));
        Assert.Equal(replies, pages.SelectMany(p => p.Items).Select(r => r.Id));
    }

    [Fact]
    public async Task Notifications_ANewOneBetweenPagesDoesNotShiftThem_AndReadingDoesNotEither()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("busy");
        for (var i = 1; i <= 5; i++)
            await bob.ReplyAsync(post.Id, $"reply {i}");
        var first = await alice.NotificationsPageAsync(take: 2);
        Assert.Equal(new[] { "reply 5", "reply 4" }, first.Items.Select(n => n.PostContent));

        await bob.ReplyAsync(post.Id, "reply 6");                  // skip/take would repeat "reply 4" now
        await alice.MarkNotificationsReadAsync();

        var second = await alice.NotificationsPageAsync(first.NextCursor, 2);
        Assert.Equal(new[] { "reply 3", "reply 2" }, second.Items.Select(n => n.PostContent));
        var third = await alice.NotificationsPageAsync(second.NextCursor, 2);
        Assert.Equal(new[] { "reply 1" }, third.Items.Select(n => n.PostContent));
        Assert.Null(third.NextCursor);
    }

    [Fact]
    public async Task Notifications_ThatDisappearBetweenPages_SkipNothing()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("busy");
        var replies = new List<int>();
        for (var i = 1; i <= 5; i++)
            replies.Add((await bob.ReplyAsync(post.Id, $"reply {i}")).Id);
        var first = await alice.NotificationsPageAsync(take: 2);   // replies 5,4

        await (await bob.DeleteAsync($"/api/posts/{replies[4]}")).ShouldBeAsync(HttpStatusCode.NoContent);   // its notification goes with it
        await (await bob.DeleteAsync($"/api/posts/{replies[3]}")).ShouldBeAsync(HttpStatusCode.NoContent);

        var second = await alice.NotificationsPageAsync(first.NextCursor, 2);
        Assert.Equal(new[] { "reply 3", "reply 2" }, second.Items.Select(n => n.PostContent));
    }

    [Fact]
    public async Task ACursorPastTheEnd_GivesAnEmptyLastPage()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("root");
        var reply = await bob.ReplyAsync(post.Id, "only reply");

        var page = await api.Anonymous.GetPageAsync<PostResponse>($"/api/posts/{post.Id}/replies", Base64Url(reply.Id.ToString()));

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    // ---- bad input -------------------------------------------------------------------------------------------

    public static IEnumerable<object[]> BadCursorsForEveryList() => new[]
    {
        "garbage", "!!!", "%%%", new string('a', 200),
        Base64Url("abc"), Base64Url("-1"), Base64Url("1.5"), Base64Url(" 1"), Base64Url("1e3"),
        Base64Url("99999999999999999999"), Base64Url("1:2:3:4"), Base64Url("1:"),
        Base64Url("1:2:-3"), Base64Url("1:x:3"), Base64Url("1:2:99999999999"),
    }.Select(cursor => new object[] { cursor });

    public static IEnumerable<object[]> AllPagedEndpoints() => new[]
    {
        Feed, "/api/posts/user/1", "/api/posts/user/1/replies", "/api/posts/1/replies", "/api/notifications",
    }.Select(path => new object[] { path });

    [Theory]
    [MemberData(nameof(AllPagedEndpoints))]
    public async Task ABrokenCursor_IsA400WithAMessage_NeverAServerError(string path)
    {
        var me = await api.RegisterAsync("me");

        foreach (var cursor in BadCursorsForEveryList().Select(c => (string)c[0]))
        {
            var response = await me.GetAsync(PagingExtensions.PagePath(path, cursor));

            await response.ShouldBeAsync(HttpStatusCode.BadRequest);
            Assert.Equal("Invalid cursor", await response.ReadMessageAsync());
        }
    }

    [Fact]
    public async Task ACursorOfTheWrongKind_IsRejected()
    {
        var me = await api.RegisterAsync("me");
        await PostIdsAsync(me, 3);
        var timelineCursor = (await me.FeedPageAsync(take: 1)).NextCursor!;      // date + post + reposter
        var idCursor = Base64Url("5");

        // a feed cursor on a list that is ordered by id, and an id cursor on the feed
        await (await me.GetAsync(PagingExtensions.PagePath("/api/posts/1/replies", timelineCursor))).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await me.GetAsync(PagingExtensions.PagePath("/api/notifications", timelineCursor))).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await me.GetAsync(PagingExtensions.PagePath(Feed, idCursor))).ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ADateBeyondTheCalendar_InAFeedCursor_IsRejectedNotAServerError()
    {
        var me = await api.RegisterAsync("me");

        var tooLarge = Base64Url($"{long.MaxValue}:1:0");
        var response = await me.GetAsync(PagingExtensions.PagePath(Feed, tooLarge));

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ACursorThatWasNeverIssued_ButIsWellFormed_IsJustAPosition()
    {
        var me = await api.RegisterAsync("me");
        var ids = await PostIdsAsync(me, 3);
        var farFuture = Base64Url($"{new DateTime(2999, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks}:1:0");
        var farPast = Base64Url($"{new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks}:1:0");

        var fromFuture = await me.FeedPageAsync(farFuture);
        var fromPast = await me.FeedPageAsync(farPast);

        Assert.Equal(ids.Count, fromFuture.Items.Count);   // everything is older than the year 2999
        Assert.Empty(fromPast.Items);                      // nothing is older than the year 2000
    }
}
