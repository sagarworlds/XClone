using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using XCloneAPI.DTOs;
using XCloneAPI.Models;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>Mentions (@username) in posts and replies: who is named, who is told, and what happens when posts go.</summary>
[Collection(ApiCollection.Name)]
public class MentionTests(ApiFixture api)
{
    private static List<NotificationResponse> Mentions(IEnumerable<NotificationResponse> notifications) =>
        notifications.Where(n => n.Type == "mention").ToList();

    private Task<int> RowsOf(int postId) => api.WithDbAsync(db => db.PostMentions.CountAsync(m => m.PostId == postId));

    private async Task<User> AddOldAccountAsync(string username)
    {
        return await api.WithDbAsync(async db =>
        {
            var user = new User { Username = username, Email = $"{username}@old.example.test", PasswordHash = "x", DisplayName = username };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user;
        });
    }

    // ---- naming people ----------------------------------------------------------------------------------------

    [Fact]
    public async Task APostThatNamesSomeone_ListsThemInMentions_AndTellsThem()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");

        var post = await alice.CreatePostAsync($"Hello @{bob.Username}, how are you?");

        Assert.Equal(new[] { bob.Username }, post.Mentions);
        var told = Assert.Single(Mentions(await bob.NotificationsAsync()));
        Assert.Equal(alice.Id, told.Actor.Id);
        Assert.Equal(post.Id, told.PostId);
        Assert.Contains("how are you", told.PostContent);
        Assert.False(told.IsRead);
        Assert.Equal(1, await bob.UnreadCountAsync());
    }

    [Fact]
    public async Task ANameIsFoundWithoutRegardToCase_AndListedAsTheProfileSpellsIt()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");

        var post = await alice.CreatePostAsync($"Shouting at @{bob.Username.ToUpperInvariant()}");

        Assert.Equal(new[] { bob.Username }, post.Mentions);
        Assert.Single(Mentions(await bob.NotificationsAsync()));
    }

    [Fact]
    public async Task ANameThatBelongsToNobody_IsNotAMention_AndNobodyIsTold()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");

        var post = await alice.CreatePostAsync($"Hello @nobody_{Guid.NewGuid():N} and me@{bob.Username}.example");

        Assert.Empty(post.Mentions);
        Assert.Equal(0, await RowsOf(post.Id));
        Assert.Empty(Mentions(await bob.NotificationsAsync()));
    }

    [Fact]
    public async Task ANameGluedToAWord_IsNotAMention()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");

        var post = await alice.CreatePostAsync($"mail me@{bob.Username} or a@@{bob.Username} or #tag@{bob.Username}");

        Assert.Empty(post.Mentions);
        Assert.Empty(await bob.NotificationsAsync());
    }

    [Fact]
    public async Task NamingYourself_ListsYou_ButTellsNobody()
    {
        var alice = await api.RegisterAsync("alice");

        var post = await alice.CreatePostAsync($"Talking about @{alice.Username}");

        Assert.Equal(new[] { alice.Username }, post.Mentions);
        Assert.Empty(await alice.NotificationsAsync());
        Assert.Equal(0, await alice.UnreadCountAsync());
    }

    [Fact]
    public async Task NamingSomeoneTwice_InAnyCase_IsOneMentionAndOneNotification()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");

        var post = await alice.CreatePostAsync($"@{bob.Username} @{bob.Username.ToUpperInvariant()} @{bob.Username}");

        Assert.Equal(new[] { bob.Username }, post.Mentions);
        Assert.Equal(1, await RowsOf(post.Id));
        Assert.Single(Mentions(await bob.NotificationsAsync()));
    }

    [Fact]
    public async Task NamingSeveralPeople_ListsAllOfThem_AndTellsEachOnce()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");

        var post = await alice.CreatePostAsync($"@{carol.Username} and @{bob.Username} and @{carol.Username}");

        Assert.Equal(new[] { bob.Username, carol.Username }.Order(StringComparer.Ordinal), post.Mentions);   // in a fixed order
        Assert.Single(Mentions(await bob.NotificationsAsync()));
        Assert.Single(Mentions(await carol.NotificationsAsync()));
        Assert.Empty(await alice.NotificationsAsync());
    }

    [Fact]
    public async Task OnlyTheFirstTenNamesCount()
    {
        var alice = await api.RegisterAsync("alice");
        var people = new List<TestUser>();
        for (var i = 0; i < 11; i++) people.Add(await api.RegisterAsync($"crowd{i}"));

        var post = await alice.CreatePostAsync(string.Join(" ", people.Select(p => "@" + p.Username)));

        Assert.Equal(10, post.Mentions.Length);
        Assert.Equal(10, await RowsOf(post.Id));
        Assert.Single(Mentions(await people[9].NotificationsAsync()));
        Assert.Empty(await people[10].NotificationsAsync());   // the eleventh is not told
    }

    [Fact]
    public async Task UnknownNamesUseUpTheTenToo_LikeTheParserSaysBeforeAnyLookup()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var unknown = string.Join(" ", Enumerable.Range(1, 10).Select(i => $"@ghost{i:00}_{Guid.NewGuid():N}"[..20]));

        var post = await alice.CreatePostAsync($"{unknown} @{bob.Username}");

        Assert.Empty(post.Mentions);   // bob is the eleventh name
        Assert.Empty(await bob.NotificationsAsync());
    }

    // ---- replies ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AReplyThatNamesThePostsAuthor_TellsThemOnce_AsAReply()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("my post");

        var reply = await bob.ReplyAsync(post.Id, $"@{alice.Username} thanks");

        Assert.Equal(new[] { alice.Username }, reply.Mentions);
        var told = Assert.Single(await alice.NotificationsAsync());
        Assert.Equal("reply", told.Type);
        Assert.Equal(reply.Id, told.PostId);
    }

    [Fact]
    public async Task AReplyThatNamesSomeoneElse_TellsThem_AsAMention_AndThePostsAuthorAsAReply()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var post = await alice.CreatePostAsync("my post");

        var reply = await bob.ReplyAsync(post.Id, $"@{carol.Username} look at this");

        var forCarol = Assert.Single(await carol.NotificationsAsync());
        Assert.Equal("mention", forCarol.Type);
        Assert.Equal(reply.Id, forCarol.PostId);
        Assert.Equal("reply", Assert.Single(await alice.NotificationsAsync()).Type);
    }

    [Fact]
    public async Task AReplyToYourOwnPost_ThatNamesYou_TellsNobody()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("my post");

        var reply = await alice.ReplyAsync(post.Id, $"note to @{alice.Username}");

        Assert.Equal(new[] { alice.Username }, reply.Mentions);
        Assert.Empty(await alice.NotificationsAsync());
    }

    [Fact]
    public async Task ARepliesMentions_ShowInTheThread_AndOnTheProfileRepliesTab()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var post = await alice.CreatePostAsync("root");
        var reply = await bob.ReplyAsync(post.Id, $"@{carol.Username}");

        var inThread = await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/{post.Id}/replies");
        var onProfile = await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/user/{bob.Id}/replies");

        Assert.Equal(new[] { carol.Username }, Assert.Single(inThread).Mentions);
        Assert.Equal(new[] { carol.Username }, Assert.Single(onProfile, p => p.Id == reply.Id).Mentions);
    }

    // ---- the lists --------------------------------------------------------------------------------------------

    [Fact]
    public async Task MentionsAreInEveryListThatShowsPosts_ForAnyone()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        await carol.FollowAsync(alice);
        var tag = "t" + Guid.NewGuid().ToString("N")[..12];
        var post = await alice.CreatePostAsync($"#{tag} hello @{bob.Username}");
        var expected = new[] { bob.Username };

        Assert.Equal(expected, (await api.Anonymous.GetFromJsonAsync<PostResponse>($"/api/posts/{post.Id}", TestUser.Json))!.Mentions);
        Assert.Equal(expected, Assert.Single(await carol.FeedAsync(), p => p.Id == post.Id).Mentions);
        Assert.Equal(expected, Assert.Single(await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/user/{alice.Id}"), p => p.Id == post.Id).Mentions);
        Assert.Equal(expected, Assert.Single(await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/hashtag/{tag}")).Mentions);
    }

    [Fact]
    public async Task ARepostedPost_KeepsItsMentions()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        await carol.FollowAsync(bob);
        var post = await alice.CreatePostAsync($"@{bob.Username} you should see this");
        await bob.ToggleRetweetAsync(post.Id);

        var entry = Assert.Single(await carol.FeedAsync(), p => p.Id == post.Id);

        Assert.NotNull(entry.RetweetedBy);
        Assert.Equal(new[] { bob.Username }, entry.Mentions);
    }

    [Fact]
    public async Task APostWithoutMentions_HasAnEmptyList_NeverNull()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("nobody in here");

        var fetched = await alice.GetPostAsync(post.Id);

        Assert.Empty(post.Mentions);
        Assert.Empty(fetched.Mentions);
        var raw = await (await alice.GetAsync($"/api/posts/{post.Id}")).Content.ReadAsStringAsync();
        Assert.Contains("\"mentions\":[]", raw);
    }

    // ---- when posts go ----------------------------------------------------------------------------------------

    [Fact]
    public async Task DeletingThePost_TakesTheNotificationAndTheRowsWithIt()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync($"@{bob.Username} psst");
        Assert.Equal(1, await bob.UnreadCountAsync());

        await (await alice.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        Assert.Empty(await bob.NotificationsAsync());
        Assert.Equal(0, await bob.UnreadCountAsync());
        Assert.Equal(0, await RowsOf(post.Id));
    }

    [Fact]
    public async Task DeletingAPost_TakesTheMentionsOfItsRepliesWithIt()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var post = await alice.CreatePostAsync("root");
        var reply = await bob.ReplyAsync(post.Id, $"@{carol.Username} hi");

        await (await alice.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        Assert.Equal(0, await RowsOf(reply.Id));
        Assert.Empty(Mentions(await carol.NotificationsAsync()));
    }

    // ---- accounts that differ only by case (older ones) ------------------------------------------------------

    [Fact]
    public async Task WhenTwoOldAccountsDifferOnlyByCase_TheExactSpellingWins_ElseTheOldest()
    {
        var alice = await api.RegisterAsync("alice");
        var stem = "dup_" + Guid.NewGuid().ToString("N")[..10];
        var older = await AddOldAccountAsync(stem.ToUpperInvariant());
        var newer = await AddOldAccountAsync(stem);

        var exact = await alice.CreatePostAsync($"@{stem}");
        var exactUpper = await alice.CreatePostAsync($"@{stem.ToUpperInvariant()}");
        var neither = await alice.CreatePostAsync($"@{stem[..4]}{stem[4..].ToUpperInvariant()}");   // matches both, spelled as neither

        Assert.Equal(new[] { newer.Username }, exact.Mentions);
        Assert.Equal(new[] { older.Username }, exactUpper.Mentions);
        Assert.Equal(new[] { older.Username }, neither.Mentions);   // the oldest account
    }
}
