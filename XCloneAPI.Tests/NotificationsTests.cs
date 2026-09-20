using System.Net;
using System.Net.Http.Json;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

[Collection(ApiCollection.Name)]
public class NotificationsTests(ApiFixture api)
{
    // ---- replies --------------------------------------------------------------------------------------------

    [Fact]
    public async Task ARepliesNotifiesTheAuthorOfThePostRepliedTo_AndNobodyElse()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("hello");

        var reply = await bob.ReplyAsync(post.Id, "hi alice");

        var notification = Assert.Single(await alice.NotificationsAsync());
        Assert.Equal("reply", notification.Type);
        Assert.Equal(bob.Id, notification.Actor.Id);
        Assert.Equal(bob.Username, notification.Actor.Username);
        Assert.Equal(reply.Id, notification.PostId);           // opens the reply itself
        Assert.Equal("hi alice", notification.PostContent);
        Assert.False(notification.IsRead);
        Assert.True(notification.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.Empty(await bob.NotificationsAsync());          // the actor is not notified about their own action
    }

    [Fact]
    public async Task ReplyingToYourOwnPost_DoesNotNotifyYou()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("a thread");

        await alice.ReplyAsync(post.Id, "adding to my own thread");

        Assert.Empty(await alice.NotificationsAsync());
        Assert.Equal(0, await alice.UnreadCountAsync());
    }

    [Fact]
    public async Task ReplyingToAReply_NotifiesTheAuthorOfThatReply_NotTheOriginalPoster()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var post = await alice.CreatePostAsync("root");
        var reply = await bob.ReplyAsync(post.Id, "bob's reply");
        await alice.MarkNotificationsReadAsync();

        await carol.ReplyAsync(reply.Id, "carol answers bob");

        var forBob = Assert.Single(await bob.NotificationsAsync());
        Assert.Equal(carol.Id, forBob.Actor.Id);
        Assert.Single(await alice.NotificationsAsync());       // still only bob's reply from before
        Assert.Equal(0, await alice.UnreadCountAsync());
    }

    [Fact]
    public async Task EveryReplyNotifies_NewestFirst()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("popular");

        await bob.ReplyAsync(post.Id, "first");
        await bob.ReplyAsync(post.Id, "second");
        await bob.ReplyAsync(post.Id, "third");

        Assert.Equal(new[] { "third", "second", "first" }, (await alice.NotificationsAsync()).Select(n => n.PostContent));
        Assert.Equal(3, await alice.UnreadCountAsync());
    }

    [Fact]
    public async Task DeletingAReply_TakesItsNotificationAway()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("x");
        var reply = await bob.ReplyAsync(post.Id, "regrets");
        Assert.Equal(1, await alice.UnreadCountAsync());

        await (await bob.DeleteAsync($"/api/posts/{reply.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        Assert.Empty(await alice.NotificationsAsync());
        Assert.Equal(0, await alice.UnreadCountAsync());
    }

    [Fact]
    public async Task DeletingThePost_TakesAllNotificationsAboutItAway()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var post = await alice.CreatePostAsync("doomed");
        await bob.ReplyAsync(post.Id, "a reply");
        await carol.ToggleRetweetAsync(post.Id);
        var other = await alice.CreatePostAsync("survivor");
        await bob.ToggleRetweetAsync(other.Id);
        Assert.Equal(3, (await alice.NotificationsAsync()).Count);

        await (await alice.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        var remaining = Assert.Single(await alice.NotificationsAsync());
        Assert.Equal(other.Id, remaining.PostId);
    }

    // ---- reposts --------------------------------------------------------------------------------------------

    [Fact]
    public async Task ARepostNotifiesTheAuthor()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("worth sharing");

        await bob.ToggleRetweetAsync(post.Id);

        var notification = Assert.Single(await alice.NotificationsAsync());
        Assert.Equal("repost", notification.Type);
        Assert.Equal(bob.Id, notification.Actor.Id);
        Assert.Equal(post.Id, notification.PostId);            // the post that was reposted
        Assert.Equal("worth sharing", notification.PostContent);
        Assert.False(notification.IsRead);
        Assert.Empty(await bob.NotificationsAsync());
    }

    [Fact]
    public async Task RepostingYourOwnPost_DoesNotNotifyYou()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("mine");

        await alice.ToggleRetweetAsync(post.Id);

        Assert.Empty(await alice.NotificationsAsync());
    }

    [Fact]
    public async Task UndoingARepost_TakesTheNotificationBack_AndRepostingAgainNotifiesOnce()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("flip flop");

        await bob.ToggleRetweetAsync(post.Id);
        Assert.Equal(1, await alice.UnreadCountAsync());

        await bob.ToggleRetweetAsync(post.Id);                 // undo
        Assert.Empty(await alice.NotificationsAsync());
        Assert.Equal(0, await alice.UnreadCountAsync());

        await bob.ToggleRetweetAsync(post.Id);                 // again
        await bob.ToggleRetweetAsync(post.Id);                 // undo
        await bob.ToggleRetweetAsync(post.Id);                 // and again
        Assert.Single(await alice.NotificationsAsync());       // never more than one per person and post
    }

    [Fact]
    public async Task UndoingARepost_OnlyRemovesThatPersonsNotification()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var post = await alice.CreatePostAsync("shared by two");
        await bob.ToggleRetweetAsync(post.Id);
        await carol.ToggleRetweetAsync(post.Id);

        await bob.ToggleRetweetAsync(post.Id);

        var left = Assert.Single(await alice.NotificationsAsync());
        Assert.Equal(carol.Id, left.Actor.Id);
    }

    [Fact]
    public async Task RepostingAReply_NotifiesTheAuthorOfTheReply()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var post = await alice.CreatePostAsync("root");
        var reply = await bob.ReplyAsync(post.Id, "quotable");
        await alice.MarkNotificationsReadAsync();

        await carol.ToggleRetweetAsync(reply.Id);

        var forBob = Assert.Single(await bob.NotificationsAsync());
        Assert.Equal("repost", forBob.Type);
        Assert.Equal(reply.Id, forBob.PostId);
        Assert.Equal(0, await alice.UnreadCountAsync());
    }

    // ---- reading --------------------------------------------------------------------------------------------

    [Fact]
    public async Task RepliesAndRepostsAreListedTogether_NewestFirst()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("mixed");

        await bob.ToggleRetweetAsync(post.Id);
        await bob.ReplyAsync(post.Id, "and a reply");

        Assert.Equal(new[] { "reply", "repost" }, (await alice.NotificationsAsync()).Select(n => n.Type));
    }

    [Fact]
    public async Task MarkingAsRead_ClearsTheCount_KeepsTheNotifications_AndOnlyTouchesYourOwn()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("from alice");
        var bobsPost = await bob.CreatePostAsync("from bob");
        await bob.ReplyAsync(post.Id, "to alice");
        await alice.ReplyAsync(bobsPost.Id, "to bob");

        await alice.MarkNotificationsReadAsync();

        Assert.Equal(0, await alice.UnreadCountAsync());
        var kept = Assert.Single(await alice.NotificationsAsync());
        Assert.True(kept.IsRead);
        Assert.Equal(1, await bob.UnreadCountAsync());         // alice's read-all did not touch bob's

        await alice.MarkNotificationsReadAsync();              // harmless when there is nothing left
        Assert.Equal(0, await alice.UnreadCountAsync());
    }

    [Fact]
    public async Task NotificationsThatArriveAfterMarkingAsRead_AreUnread()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("x");
        await bob.ReplyAsync(post.Id, "old");
        await alice.MarkNotificationsReadAsync();

        await bob.ReplyAsync(post.Id, "new");

        Assert.Equal(1, await alice.UnreadCountAsync());
        Assert.Equal(new[] { false, true }, (await alice.NotificationsAsync()).Select(n => n.IsRead));
    }

    [Fact]
    public async Task EveryoneOnlySeesTheirOwnNotifications()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var post = await alice.CreatePostAsync("for alice");
        await bob.ReplyAsync(post.Id, "secret-reply-text");

        Assert.Single(await alice.NotificationsAsync());
        Assert.Empty(await carol.NotificationsAsync());
        Assert.Equal(0, await carol.UnreadCountAsync());
    }

    [Fact]
    public async Task Notifications_NeverContainAnEmailOrAPasswordHash()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("privacy");
        await bob.ReplyAsync(post.Id, "hi");
        await bob.ToggleRetweetAsync(post.Id);

        var body = await (await alice.GetAsync("/api/notifications")).Content.ReadAsStringAsync();

        Assert.DoesNotContain(bob.Email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(alice.Email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AQAAAA", body);
    }

    // ---- paging ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Paging_WalksThroughEverythingInOrder_WithoutGapsOrRepeats()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("busy");
        for (var i = 1; i <= 7; i++)
            await bob.ReplyAsync(post.Id, $"reply {i}");

        var pages = await alice.ReadAllPagesAsync<NotificationResponse>("/api/notifications", take: 3);

        Assert.Equal(new[] { 3, 3, 1 }, pages.Select(p => p.Items.Count));
        Assert.Equal(Enumerable.Range(1, 7).Reverse().Select(i => $"reply {i}"), pages.SelectMany(p => p.Items).Select(n => n.PostContent));
        Assert.Null(pages[^1].NextCursor);
    }

    [Fact]
    public async Task PageSize_IsClamped()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("flooded");
        for (var i = 0; i < 55; i++)
            await bob.ReplyAsync(post.Id, $"r{i}");

        var biggest = await alice.NotificationsPageAsync(take: 1000);
        Assert.Equal(50, biggest.Items.Count);
        Assert.Single(await alice.NotificationsAsync(take: 0));
        Assert.Single(await alice.NotificationsAsync(take: -5));
        Assert.Equal(5, (await alice.NotificationsPageAsync(biggest.NextCursor, 50)).Items.Count);
        Assert.Equal(20, (await alice.GetItemsAsync<NotificationResponse>("/api/notifications")).Count);   // the default page
    }

    // ---- access ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task EveryEndpoint_RequiresLogin()
    {
        await (await api.Anonymous.GetAsync("/api/notifications")).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await api.Anonymous.GetAsync("/api/notifications/unread-count")).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await api.Anonymous.PostAsJsonAsync("/api/notifications/read-all", new { })).ShouldBeAsync(HttpStatusCode.Unauthorized);
    }
}
