using System.Net;
using Microsoft.EntityFrameworkCore;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>Hashtags in posts: which posts a tag finds, in what order, and what happens when posts change or go.</summary>
[Collection(ApiCollection.Name)]
public class HashtagTests(ApiFixture api)
{
    /// <summary>A tag no other test uses (the database is shared), starting with a letter.</summary>
    private static string NewTag() => "t" + Guid.NewGuid().ToString("N")[..14];

    private static string TagPath(string tag) => $"/api/posts/hashtag/{tag}";

    private Task<List<PostResponse>> FoundBy(string tag, TestUser? reader = null) =>
        (reader?.Client ?? api.Anonymous).GetItemsAsync<PostResponse>(TagPath(tag), take: 50);

    private Task<int> RowsOf(int postId) => api.WithDbAsync(db => db.PostHashtags.CountAsync(h => h.PostId == postId));

    // ---- finding posts by tag ---------------------------------------------------------------------------------

    [Fact]
    public async Task APostWithATag_IsFoundByThatTag_WhateverTheCaseOfEitherSide()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = NewTag();
        var post = await alice.CreatePostAsync($"Watching the #{tag.ToUpperInvariant()} tonight");

        foreach (var address in new[] { tag, tag.ToUpperInvariant(), "%23" + tag, "%23" + tag.ToUpperInvariant() })
            Assert.Equal(new[] { post.Id }, (await FoundBy(address)).Select(p => p.Id));
    }

    [Fact]
    public async Task RepliesAreFoundToo_NotOnlyTopLevelPosts()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var tag = NewTag();
        var root = await alice.CreatePostAsync("a plain post");
        var reply = await bob.ReplyAsync(root.Id, $"replying #{tag}");

        var found = await FoundBy(tag);

        var only = Assert.Single(found);
        Assert.Equal(reply.Id, only.Id);
        Assert.Equal(root.Id, only.ParentPostId);
    }

    [Fact]
    public async Task NewestFirst_AcrossAuthors()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var tag = NewTag();
        var first = await alice.CreatePostAsync($"one #{tag}");
        var second = await bob.CreatePostAsync($"two #{tag}");
        var third = await alice.CreatePostAsync($"three #{tag}");

        Assert.Equal(new[] { third.Id, second.Id, first.Id }, (await FoundBy(tag)).Select(p => p.Id));
    }

    [Fact]
    public async Task EachTagOfAPost_FindsIt_AndOnlyItsOwnPosts()
    {
        var alice = await api.RegisterAsync("alice");
        var one = NewTag();
        var two = NewTag();
        var both = await alice.CreatePostAsync($"#{one} and #{two}");
        var onlyOne = await alice.CreatePostAsync($"just #{one}");

        Assert.Equal(new[] { onlyOne.Id, both.Id }, (await FoundBy(one)).Select(p => p.Id));
        Assert.Equal(new[] { both.Id }, (await FoundBy(two)).Select(p => p.Id));
    }

    [Fact]
    public async Task ATagNobodyUsed_IsAnEmptyPage_NotAnError()
    {
        var page = await api.Anonymous.GetPageAsync<PostResponse>(TagPath(NewTag()));

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ATextThatOnlyLooksLikeATag_FindsNothing()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = NewTag();
        await alice.CreatePostAsync($"glued to a word: abc#{tag} and a number #2026 and a double ##{tag}");

        Assert.Empty(await FoundBy(tag));
    }

    [Fact]
    public async Task APostWithoutTags_HasNoRows()
    {
        var alice = await api.RegisterAsync("alice");

        var post = await alice.CreatePostAsync("nothing to see, not a #2026 in sight");

        Assert.Equal(0, await RowsOf(post.Id));
    }

    // ---- what an address may be -------------------------------------------------------------------------------

    [Theory]
    [InlineData("2026")]
    [InlineData("a-b")]
    [InlineData("a.b")]
    [InlineData("%20")]
    [InlineData("a%20b")]
    [InlineData("%23%23a")]
    [InlineData("%23")]
    [InlineData("_")]
    [InlineData("%F0%9F%98%80")]   // an emoji
    public async Task AnAddressThatCannotBeATag_IsA400(string address)
    {
        var response = await api.Anonymous.GetAsync(TagPath(address));

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.Equal("Invalid hashtag", await response.ReadMessageAsync());
    }

    [Fact]
    public async Task ATagOfFiftyLetters_Works_AndOneOfFiftyOneIsRefused()
    {
        var alice = await api.RegisterAsync("alice");
        var fifty = "z" + Guid.NewGuid().ToString("N")[..30] + new string('q', 19);
        Assert.Equal(50, fifty.Length);
        var post = await alice.CreatePostAsync($"long one #{fifty}");

        Assert.Equal(new[] { post.Id }, (await FoundBy(fifty)).Select(p => p.Id));
        await (await api.Anonymous.GetAsync(TagPath(fifty + "q"))).ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ANonLatinTag_IsFound()
    {
        var alice = await api.RegisterAsync("alice");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var post = await alice.CreatePostAsync($"東京の夜 #東京{suffix} #Ελληνικά{suffix}");

        Assert.Equal(new[] { post.Id }, (await FoundBy(Uri.EscapeDataString("東京" + suffix))).Select(p => p.Id));
        Assert.Equal(new[] { post.Id }, (await FoundBy(Uri.EscapeDataString("ΕΛΛΗΝΙΚΆ" + suffix))).Select(p => p.Id));
    }

    // ---- what is in the list ----------------------------------------------------------------------------------

    [Fact]
    public async Task AnyoneCanLook_AndLikesAreRelativeToTheReader()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var tag = NewTag();
        var post = await alice.CreatePostAsync($"like me #{tag}");
        await bob.ToggleLikeAsync(post.Id);

        Assert.False(Assert.Single(await FoundBy(tag)).IsLiked);
        Assert.False(Assert.Single(await FoundBy(tag, alice)).IsLiked);
        Assert.True(Assert.Single(await FoundBy(tag, bob)).IsLiked);
        Assert.Equal(1, Assert.Single(await FoundBy(tag)).LikesCount);
    }

    [Fact]
    public async Task ARepostDoesNotAddAnotherEntry_TheTagFindsThePostOnce()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var tag = NewTag();
        var post = await alice.CreatePostAsync($"repost me #{tag}");
        await bob.ToggleRetweetAsync(post.Id);

        var found = Assert.Single(await FoundBy(tag));

        Assert.Equal(post.Id, found.Id);
        Assert.Null(found.RetweetedBy);
        Assert.Equal(1, found.RetweetsCount);
    }

    [Fact]
    public async Task NoResponseCarriesAnEmail()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = NewTag();
        await alice.CreatePostAsync($"private? #{tag}");

        var body = await (await alice.GetAsync(TagPath(tag))).Content.ReadAsStringAsync();

        Assert.DoesNotContain(alice.Email, body, StringComparison.OrdinalIgnoreCase);
    }

    // ---- paging -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Pages_CoverEveryPostOnce_AndTheLastHasNoCursor()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = NewTag();
        var ids = new List<int>();
        for (var i = 0; i < 5; i++) ids.Add((await alice.CreatePostAsync($"post {i} #{tag}")).Id);

        var pages = await api.Anonymous.ReadAllPagesAsync<PostResponse>(TagPath(tag), take: 2);

        Assert.Equal(new[] { 2, 2, 1 }, pages.Select(p => p.Items.Count));
        Assert.Null(pages[^1].NextCursor);
        Assert.Equal(Enumerable.Reverse(ids), pages.SelectMany(p => p.Items).Select(p => p.Id));
    }

    [Fact]
    public async Task ANewPostAtTheTop_AndADeletedOneBeyondTheCursor_DoNotShiftTheNextPage()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = NewTag();
        var ids = new List<int>();
        for (var i = 0; i < 6; i++) ids.Add((await alice.CreatePostAsync($"post {i} #{tag}")).Id);
        var first = await api.Anonymous.GetPageAsync<PostResponse>(TagPath(tag), take: 2);   // 5 and 4 (indexes)

        await alice.CreatePostAsync($"late #{tag}");
        await (await alice.DeleteAsync($"/api/posts/{ids[1]}")).ShouldBeAsync(HttpStatusCode.NoContent);
        var second = await api.Anonymous.GetPageAsync<PostResponse>(TagPath(tag), first.NextCursor, take: 50);

        Assert.Equal(new[] { ids[3], ids[2], ids[0] }, second.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task ThePageSize_IsClamped_NotRejected()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = NewTag();
        for (var i = 0; i < 3; i++) await alice.CreatePostAsync($"post {i} #{tag}");

        Assert.Single((await api.Anonymous.GetPageAsync<PostResponse>(TagPath(tag), take: 0)).Items);
        Assert.Single((await api.Anonymous.GetPageAsync<PostResponse>(TagPath(tag), take: -5)).Items);
        Assert.Equal(3, (await api.Anonymous.GetPageAsync<PostResponse>(TagPath(tag), take: 100000)).Items.Count);
    }

    [Fact]
    public async Task ABrokenCursor_IsA400()
    {
        var response = await api.Anonymous.GetAsync(PagingExtensions.PagePath(TagPath(NewTag()), "garbage"));

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.Equal("Invalid cursor", await response.ReadMessageAsync());
    }

    // ---- how tags are recorded --------------------------------------------------------------------------------

    [Fact]
    public async Task TheSameTagSeveralTimesInAPost_IsOneRow_AndOneEntry()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = NewTag();

        var post = await alice.CreatePostAsync($"#{tag} #{tag.ToUpperInvariant()} #{tag}");

        Assert.Equal(1, await RowsOf(post.Id));
        Assert.Single(await FoundBy(tag));
    }

    [Fact]
    public async Task OnlyTheFirstTenTagsOfAPostAreRecorded()
    {
        var alice = await api.RegisterAsync("alice");
        var tags = Enumerable.Range(1, 12).Select(_ => NewTag()).ToList();

        var post = await alice.CreatePostAsync(string.Join(" ", tags.Select(t => "#" + t)));

        Assert.Equal(10, await RowsOf(post.Id));
        Assert.Single(await FoundBy(tags[9]));
        Assert.Empty(await FoundBy(tags[10]));
        Assert.Empty(await FoundBy(tags[11]));
    }

    [Fact]
    public async Task TagsAreStoredInLowerCase()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = NewTag();
        var post = await alice.CreatePostAsync($"#{tag.ToUpperInvariant()}");

        var stored = await api.WithDbAsync(db => db.PostHashtags.Where(h => h.PostId == post.Id).Select(h => h.Tag).ToListAsync());

        Assert.Equal(new[] { tag }, stored);
    }

    // ---- when posts go ----------------------------------------------------------------------------------------

    [Fact]
    public async Task DeletingAPost_TakesItOutOfTheTag_AndItsRowsAway()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = NewTag();
        var kept = await alice.CreatePostAsync($"stays #{tag}");
        var doomed = await alice.CreatePostAsync($"goes #{tag}");

        await (await alice.DeleteAsync($"/api/posts/{doomed.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        Assert.Equal(new[] { kept.Id }, (await FoundBy(tag)).Select(p => p.Id));
        Assert.Equal(0, await RowsOf(doomed.Id));
        Assert.Equal(1, await RowsOf(kept.Id));
    }

    [Fact]
    public async Task DeletingAPost_TakesTheTagsOfItsRepliesWithIt()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var tag = NewTag();
        var root = await alice.CreatePostAsync($"root #{tag}");
        var reply = await bob.ReplyAsync(root.Id, $"reply #{tag}");
        var deepReply = await alice.ReplyAsync(reply.Id, $"deeper #{tag}");

        await (await alice.DeleteAsync($"/api/posts/{root.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        Assert.Empty(await FoundBy(tag));
        Assert.Equal(0, await RowsOf(reply.Id) + await RowsOf(deepReply.Id));
    }

    [Fact]
    public async Task ADeleteThatIsRefused_LeavesTheTag()
    {
        var alice = await api.RegisterAsync("alice");
        var mallory = await api.RegisterAsync("mallory");
        var tag = NewTag();
        var post = await alice.CreatePostAsync($"mine #{tag}");

        await (await mallory.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NotFound);

        Assert.Single(await FoundBy(tag));
    }
}
