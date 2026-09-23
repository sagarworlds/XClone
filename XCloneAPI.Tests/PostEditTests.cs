using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>Editing a post (PUT /api/posts/{id}): who may, what changes, what stays, and who is told about new mentions.</summary>
[Collection(ApiCollection.Name)]
public class PostEditTests(ApiFixture api)
{
    /// <summary>A tag no other test uses (the database is shared), starting with a letter.</summary>
    private static string NewTag() => "t" + Guid.NewGuid().ToString("N")[..14];

    private static Task<HttpResponseMessage> Edit(TestUser user, int postId, string? content) =>
        user.PutAsync($"/api/posts/{postId}", new { content });

    private static async Task<PostResponse> EditOk(TestUser user, int postId, string content)
    {
        var response = await Edit(user, postId, content);
        await response.ShouldBeAsync(HttpStatusCode.OK);
        return await response.ReadAsync<PostResponse>();
    }

    private static List<NotificationResponse> Mentions(IEnumerable<NotificationResponse> notifications) =>
        notifications.Where(n => n.Type == "mention").ToList();

    private Task<List<string>> TagsOf(int postId) =>
        api.WithDbAsync(db => db.PostHashtags.Where(h => h.PostId == postId).Select(h => h.Tag).OrderBy(t => t).ToListAsync());

    private Task<int> MentionRows(int postId) => api.WithDbAsync(db => db.PostMentions.CountAsync(m => m.PostId == postId));

    private Task<List<PostResponse>> FoundBy(string tag) =>
        api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/hashtag/{tag}", take: 50);

    // ---- who may edit, and what comes back ----------------------------------------------------------------------

    [Fact]
    public async Task TheAuthorCanChangeTheText_ItIsMarkedAsEdited_AndEveryoneSeesTheNewText()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("first version");
        Assert.Null(post.EditedAt);
        var stored = await alice.GetPostAsync(post.Id);
        var before = DateTime.UtcNow.AddSeconds(-2);

        var edited = await EditOk(alice, post.Id, "second version");
        var after = DateTime.UtcNow.AddSeconds(2);

        Assert.Equal(post.Id, edited.Id);
        Assert.Equal("second version", edited.Content);
        Assert.Equal(stored.CreatedAt, edited.CreatedAt);
        Assert.NotNull(edited.EditedAt);
        Assert.InRange(edited.EditedAt!.Value, before, after);
        Assert.Equal(edited.EditedAt, edited.UpdatedAt);
        Assert.Equal(alice.Id, edited.User.Id);

        foreach (var reader in new[] { alice.Client, api.Anonymous })
        {
            var seen = await reader.GetFromJsonAsync<PostResponse>($"/api/posts/{post.Id}", TestUser.Json);
            Assert.Equal("second version", seen!.Content);
            Assert.Equal(edited.EditedAt, seen.EditedAt);
        }
    }

    [Fact]
    public async Task APostThatWasNeverEdited_HasNoEditedMark_AnywhereItIsListed()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("as written");

        Assert.Null((await alice.GetPostAsync(post.Id)).EditedAt);
        Assert.Null(Assert.Single(await alice.FeedAsync(), p => p.Id == post.Id).EditedAt);
    }

    [Fact]
    public async Task OtherPeopleCannotEditYourPost_AndItStaysAsItWas()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("mine");

        var response = await Edit(bob, post.Id, "hijacked");

        await response.ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Equal("Post not found or unauthorized", await response.ReadMessageAsync());
        var unchanged = await alice.GetPostAsync(post.Id);
        Assert.Equal("mine", unchanged.Content);
        Assert.Null(unchanged.EditedAt);
    }

    [Fact]
    public async Task APostThatDoesNotExist_IsTheSame404()
    {
        var alice = await api.RegisterAsync("alice");

        var response = await Edit(alice, 2_000_000_000, "hello");

        await response.ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Equal("Post not found or unauthorized", await response.ReadMessageAsync());
    }

    [Fact]
    public async Task SignedOutVisitors_CannotEdit()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("mine");

        var response = await api.Anonymous.PutAsJsonAsync($"/api/posts/{post.Id}", new { content = "hijacked" });

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("mine", (await alice.GetPostAsync(post.Id)).Content);
    }

    // ---- what is accepted ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t ")]
    public async Task ATextWithNothingInIt_IsRefused_AndThePostIsUntouched(string? content)
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("keep me");

        var response = await Edit(alice, post.Id, content);

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
        var unchanged = await alice.GetPostAsync(post.Id);
        Assert.Equal("keep me", unchanged.Content);
        Assert.Null(unchanged.EditedAt);
    }

    [Fact]
    public async Task TheRefusal_NamesTheField_LikeForANewPost()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("keep me");

        var response = await Edit(alice, post.Id, "   ");

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Content", body);
    }

    [Fact]
    public async Task ThePostRequestWithoutAContentField_IsRefused()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("keep me");

        var response = await alice.PutAsync($"/api/posts/{post.Id}", new { });

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.Equal("keep me", (await alice.GetPostAsync(post.Id)).Content);
    }

    [Fact]
    public async Task TheSameLimitAsANewPostApplies_280CharactersFit_281DoNot()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("short");

        await (await Edit(alice, post.Id, new string('a', 281))).ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.Equal("short", (await alice.GetPostAsync(post.Id)).Content);

        var longest = await EditOk(alice, post.Id, new string('b', 280));
        Assert.Equal(280, longest.Content.Length);
    }

    // ---- what changes, and what does not ------------------------------------------------------------------------

    [Fact]
    public async Task SayingTheSameThingAgain_ChangesNothing_NotEvenTheEditedMark()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("unchanged");
        var stored = await alice.GetPostAsync(post.Id);

        var untouched = await EditOk(alice, post.Id, "unchanged");
        Assert.Null(untouched.EditedAt);
        Assert.Equal(stored.UpdatedAt, untouched.UpdatedAt);

        var first = await EditOk(alice, post.Id, "changed");
        var again = await EditOk(alice, post.Id, "changed");
        Assert.Equal(first.EditedAt, again.EditedAt);
        Assert.Equal(first.UpdatedAt, again.UpdatedAt);
    }

    [Fact]
    public async Task EditingAgain_MovesTheMarkForward()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("one");

        var first = await EditOk(alice, post.Id, "two");
        await Task.Delay(30);
        var second = await EditOk(alice, post.Id, "three");

        Assert.Equal("three", second.Content);
        Assert.True(second.EditedAt > first.EditedAt, "the second edit should be marked later than the first");
    }

    [Fact]
    public async Task OnlyTheTextChanges_ImagesCountsAndAuthorStay_AndSentImagesAreIgnored()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var image = await alice.UploadImageAsync();
        var other = await alice.UploadImageAsync();
        var post = await alice.CreatePostWithImagesAsync("with a picture", image);
        await bob.ToggleLikeAsync(post.Id);
        await bob.ToggleRetweetAsync(post.Id);
        await bob.ReplyAsync(post.Id, "nice");
        await alice.ToggleLikeAsync(post.Id);

        var response = await alice.PutAsync($"/api/posts/{post.Id}", new { content = "with a better caption", mediaUrls = new[] { other } });
        await response.ShouldBeAsync(HttpStatusCode.OK);
        var edited = await response.ReadAsync<PostResponse>();

        Assert.Equal("with a better caption", edited.Content);
        Assert.Equal(new[] { image }, edited.MediaUrls);
        Assert.Equal(2, edited.LikesCount);
        Assert.Equal(1, edited.RetweetsCount);
        Assert.Equal(1, edited.RepliesCount);
        Assert.True(edited.IsLiked);          // relative to the one who asked
        Assert.False(edited.IsRetweeted);
        Assert.Null(edited.ParentPostId);
    }

    [Fact]
    public async Task AnEditedPost_KeepsItsPlaceInTheTimeline_AndPagingCarriesOn()
    {
        var alice = await api.RegisterAsync("alice");
        var olderCreated = await alice.CreatePostAsync("older post");
        var older = await alice.GetPostAsync(olderCreated.Id);
        var newer = await alice.CreatePostAsync("newer post");

        var firstPage = await alice.FeedPageAsync(take: 1);
        Assert.Equal(new[] { newer.Id }, firstPage.Items.Select(p => p.Id));

        await EditOk(alice, older.Id, "older post, corrected");

        var secondPage = await alice.FeedPageAsync(firstPage.NextCursor, take: 1);
        var shown = Assert.Single(secondPage.Items);
        Assert.Equal(older.Id, shown.Id);
        Assert.Equal("older post, corrected", shown.Content);
        Assert.Equal(older.CreatedAt, shown.CreatedAt);
        Assert.Null(secondPage.NextCursor);
        Assert.Equal(new[] { newer.Id, older.Id }, (await alice.FeedAsync()).Select(p => p.Id));
    }

    [Fact]
    public async Task ARepost_ShowsTheNewText_AndOnlyTheAuthorOfTheOriginalCanEdit()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        await carol.FollowAsync(bob);
        var post = await alice.CreatePostAsync("original wording");
        await bob.ToggleRetweetAsync(post.Id);

        await (await Edit(bob, post.Id, "bob rewrites it")).ShouldBeAsync(HttpStatusCode.NotFound);
        await EditOk(alice, post.Id, "better wording");

        var entry = Assert.Single(await carol.FeedAsync());
        Assert.Equal(bob.Id, entry.RetweetedBy!.Id);
        Assert.Equal("better wording", entry.Content);
        Assert.NotNull(entry.EditedAt);
    }

    [Fact]
    public async Task RepliesCanBeEdited_ByTheirAuthorOnly_AndStayReplies()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("a question");
        var reply = await bob.ReplyAsync(post.Id, "an anser");

        await (await Edit(alice, reply.Id, "putting words in bob's mouth")).ShouldBeAsync(HttpStatusCode.NotFound);
        var edited = await EditOk(bob, reply.Id, "an answer");

        Assert.Equal(post.Id, edited.ParentPostId);
        Assert.Equal(alice.Username, edited.ReplyToUsername);
        Assert.Equal(1, (await alice.GetPostAsync(post.Id)).RepliesCount);
        var listed = Assert.Single(await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/{post.Id}/replies"));
        Assert.Equal("an answer", listed.Content);
        Assert.NotNull(listed.EditedAt);
    }

    [Fact]
    public async Task TheEditedMark_ShowsInProfilesAndHashtagPagesToo()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = NewTag();
        var post = await alice.CreatePostAsync($"draft #{tag}");

        await EditOk(alice, post.Id, $"final #{tag}");

        var onProfile = await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/user/{alice.Id}");
        Assert.NotNull(Assert.Single(onProfile).EditedAt);
        var onTagPage = Assert.Single(await FoundBy(tag));
        Assert.Equal($"final #{tag}", onTagPage.Content);
        Assert.NotNull(onTagPage.EditedAt);
    }

    // ---- hashtags follow the new text ---------------------------------------------------------------------------

    [Fact]
    public async Task TheHashtagsFollowTheNewText_OldOnesGo_NewOnesAreFound_KeptOnesStayOnce()
    {
        var alice = await api.RegisterAsync("alice");
        var (gone, kept, added) = (NewTag(), NewTag(), NewTag());
        var post = await alice.CreatePostAsync($"#{gone} #{kept} first draft");
        Assert.Equal(new[] { gone, kept }.Order(), await TagsOf(post.Id));

        await EditOk(alice, post.Id, $"#{kept} #{added} second draft");

        Assert.Equal(new[] { kept, added }.Order(), await TagsOf(post.Id));
        Assert.Empty(await FoundBy(gone));
        Assert.Equal(new[] { post.Id }, (await FoundBy(kept)).Select(p => p.Id));
        Assert.Equal(new[] { post.Id }, (await FoundBy(added)).Select(p => p.Id));
    }

    [Fact]
    public async Task ATagWrittenInAnotherCase_IsStillTheSameOneTag()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = NewTag();
        var post = await alice.CreatePostAsync($"about #{tag}");

        await EditOk(alice, post.Id, $"about #{tag.ToUpperInvariant()} today");

        Assert.Equal(new[] { tag }, await TagsOf(post.Id));
    }

    [Fact]
    public async Task TakingEveryTagOut_LeavesNone()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = NewTag();
        var post = await alice.CreatePostAsync($"loud #{tag}");

        await EditOk(alice, post.Id, "quiet now");

        Assert.Empty(await TagsOf(post.Id));
        Assert.Empty(await FoundBy(tag));
    }

    // ---- mentions follow the new text ---------------------------------------------------------------------------

    [Fact]
    public async Task NamingSomeoneInAnEdit_TellsThemOnce_EvenWhenTheTextIsEditedAgain()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("hello there");
        Assert.Empty(await bob.NotificationsAsync());

        var edited = await EditOk(alice, post.Id, $"hello there @{bob.Username}");

        Assert.Equal(new[] { bob.Username }, edited.Mentions);
        Assert.Equal(1, await MentionRows(post.Id));
        var told = Assert.Single(Mentions(await bob.NotificationsAsync()));
        Assert.Equal(alice.Id, told.Actor.Id);
        Assert.Equal(post.Id, told.PostId);
        Assert.Contains(bob.Username, told.PostContent);
        Assert.Equal(1, await bob.UnreadCountAsync());

        await EditOk(alice, post.Id, $"hello again @{bob.Username}, and welcome");   // still there: nothing new to tell
        Assert.Single(await bob.NotificationsAsync());
        Assert.Equal(1, await MentionRows(post.Id));
    }

    [Fact]
    public async Task TakingSomeoneOut_RemovesThemFromTheMentions_AndTheirNotificationWithIt()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var post = await alice.CreatePostAsync($"@{bob.Username} and @{carol.Username}");
        Assert.Single(await bob.NotificationsAsync());

        var edited = await EditOk(alice, post.Id, $"only @{carol.Username} now");

        Assert.Equal(new[] { carol.Username }, edited.Mentions);
        Assert.Equal(1, await MentionRows(post.Id));
        Assert.Empty(await bob.NotificationsAsync());
        Assert.Equal(0, await bob.UnreadCountAsync());
        Assert.Single(Mentions(await carol.NotificationsAsync()));
    }

    [Fact]
    public async Task TakingSomeoneOutOfOnePost_LeavesTheirNotificationsAboutOtherPostsAlone()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var edited = await alice.CreatePostAsync($"@{bob.Username} first");
        var other = await alice.CreatePostAsync($"@{bob.Username} second");
        Assert.Equal(2, (await bob.NotificationsAsync()).Count);

        await EditOk(alice, edited.Id, "first, without bob");

        var left = Assert.Single(await bob.NotificationsAsync());
        Assert.Equal(other.Id, left.PostId);
    }

    [Fact]
    public async Task TakingSomeoneOutOfAReply_KeepsTheReplyNotificationTheyGot()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("a question");
        var reply = await bob.ReplyAsync(post.Id, $"@{alice.Username} here is the answer");
        Assert.Equal("reply", Assert.Single(await alice.NotificationsAsync()).Type);

        var edited = await EditOk(bob, reply.Id, "here is the answer");

        Assert.Empty(edited.Mentions);
        var kept = Assert.Single(await alice.NotificationsAsync());
        Assert.Equal("reply", kept.Type);
        Assert.Equal(reply.Id, kept.PostId);
    }

    [Fact]
    public async Task NamingSomeoneAgainAfterTakingThemOut_TellsThemAgain_ButOnlyOnce()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync($"@{bob.Username}");
        await bob.MarkNotificationsReadAsync();

        await EditOk(alice, post.Id, "nobody");
        Assert.Empty(await bob.NotificationsAsync());

        await EditOk(alice, post.Id, $"@{bob.Username} after all");

        var told = Assert.Single(await bob.NotificationsAsync());
        Assert.False(told.IsRead);
        Assert.Equal(1, await bob.UnreadCountAsync());
    }

    [Fact]
    public async Task NamingYourselfInAnEdit_IsListed_ButTellsNobody()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("note to self");

        var edited = await EditOk(alice, post.Id, $"note to @{alice.Username}");

        Assert.Equal(new[] { alice.Username }, edited.Mentions);
        Assert.Empty(await alice.NotificationsAsync());
    }

    [Fact]
    public async Task NamesThatBelongToNobody_AreIgnoredInAnEdit()
    {
        var alice = await api.RegisterAsync("alice");
        var post = await alice.CreatePostAsync("hi");

        var edited = await EditOk(alice, post.Id, $"hi @nobody_{Guid.NewGuid():N}");

        Assert.Empty(edited.Mentions);
        Assert.Equal(0, await MentionRows(post.Id));
    }

    [Fact]
    public async Task InAReply_TheAuthorRepliedTo_IsNotToldTwice_ButOthersAre()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var post = await alice.CreatePostAsync("a question");
        var reply = await bob.ReplyAsync(post.Id, "an answer");
        Assert.Single(await alice.NotificationsAsync());   // the reply

        var edited = await EditOk(bob, reply.Id, $"an answer, @{alice.Username}, thanks @{carol.Username}");

        Assert.Equal(new[] { alice.Username, carol.Username }.Order(StringComparer.Ordinal), edited.Mentions);
        var forAlice = Assert.Single(await alice.NotificationsAsync());
        Assert.Equal("reply", forAlice.Type);
        Assert.Single(Mentions(await carol.NotificationsAsync()));
    }

    // ---- afterwards -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AnEditedPost_CanStillBeDeleted_WithItsTagsAndMentions()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var tag = NewTag();
        var post = await alice.CreatePostAsync("plain");
        await EditOk(alice, post.Id, $"#{tag} @{bob.Username}");
        Assert.Equal(1, await MentionRows(post.Id));

        await (await alice.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        Assert.Empty(await TagsOf(post.Id));
        Assert.Equal(0, await MentionRows(post.Id));
        Assert.Empty(await FoundBy(tag));
    }
}
