using System.Net;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>Post search (GET /api/posts/search): what matches, in what order, the #tag path, and what is refused.</summary>
[Collection(ApiCollection.Name)]
public class PostSearchTests(ApiFixture api)
{
    /// <summary>A word no other test's posts contain (the database is shared).</summary>
    private static string Token() => "zz" + Guid.NewGuid().ToString("N")[..14];

    private static string SearchPath(string query, string? cursor = null, int? take = null) =>
        PagingExtensions.PagePath($"/api/posts/search?query={Uri.EscapeDataString(query)}", cursor, take);

    private Task<PagedResponse<PostResponse>> SearchPage(TestUser user, string query, string? cursor = null, int? take = null) =>
        user.GetPageAsync<PostResponse>(SearchPath(query, cursor, take));

    private async Task<List<PostResponse>> Search(TestUser user, string query, int take = 50) =>
        (await SearchPage(user, query, take: take)).Items;

    // ---- matching -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task FindsAPostByAWordInIt_WhateverTheCaseOfEitherSide()
    {
        var alice = await api.RegisterAsync("alice");
        var word = Token();
        var post = await alice.CreatePostAsync($"Thinking about {word.ToUpperInvariant()} today");

        foreach (var query in new[] { word, word.ToUpperInvariant(), word.ToLowerInvariant() })
            Assert.Equal(new[] { post.Id }, (await Search(alice, query)).Select(p => p.Id));
    }

    [Fact]
    public async Task APrefixOfAWord_FindsThePostTheWordIsIn()
    {
        var alice = await api.RegisterAsync("alice");
        var word = Token();
        var post = await alice.CreatePostAsync($"a post about {word}");

        Assert.Equal(new[] { post.Id }, (await Search(alice, word[..^4])).Select(p => p.Id));
    }

    [Fact]
    public async Task RepliesAreFoundToo_NotOnlyTopLevelPosts()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var word = Token();
        var root = await alice.CreatePostAsync("a plain post");
        var reply = await bob.ReplyAsync(root.Id, $"a reply about {word}");

        var found = Assert.Single(await Search(alice, word));

        Assert.Equal(reply.Id, found.Id);
        Assert.Equal(root.Id, found.ParentPostId);
    }

    [Fact]
    public async Task APostThatDoesNotContainTheWord_IsNotFound()
    {
        var alice = await api.RegisterAsync("alice");
        var word = Token();
        await alice.CreatePostAsync("something else entirely");

        Assert.Empty(await Search(alice, word));
    }

    [Fact]
    public async Task AWordSplitByOtherText_DoesNotMatch()
    {
        var alice = await api.RegisterAsync("alice");
        var word = Token();
        await alice.CreatePostAsync($"{word[..4]} and {word[4..]} separately");

        Assert.Empty(await Search(alice, word));
    }

    [Fact]
    public async Task NewestFirst_AcrossAuthors()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var word = Token();
        var first = await alice.CreatePostAsync($"one {word}");
        var second = await bob.CreatePostAsync($"two {word}");
        var third = await alice.CreatePostAsync($"three {word}");

        Assert.Equal(new[] { third.Id, second.Id, first.Id }, (await Search(alice, word)).Select(p => p.Id));
    }

    [Fact]
    public async Task TreatsSqlWildcardsAsPlainText()
    {
        var alice = await api.RegisterAsync("alice");
        var word = Token();
        var post = await alice.CreatePostAsync($"exactly {word} here");

        Assert.Single(await Search(alice, word));                    // control: a plain search finds it
        Assert.Empty(await Search(alice, $"exac%{word}"));           // '%' is not "anything"
        Assert.Empty(await Search(alice, $"ex_ctly {word}"));        // '_' is not "any one character"
        Assert.Empty(await Search(alice, "'; DROP TABLE posts; --"));
        Assert.Single(await Search(alice, word));                    // the table is still there
    }

    // ---- the #tag path --------------------------------------------------------------------------------------------

    [Fact]
    public async Task AQueryThatStartsWithHash_SearchesByTag_NotByText()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = "t" + Guid.NewGuid().ToString("N")[..14];
        var tagged = await alice.CreatePostAsync($"#{tag}");
        var mentionsItInWords = await alice.CreatePostAsync($"talking about {tag} without the hash");

        var found = Assert.Single(await Search(alice, $"#{tag}"));

        Assert.Equal(tagged.Id, found.Id);
    }

    [Fact]
    public async Task TheHashSearchIsAlsoCaseInsensitive_AndFindsRepliesToo()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var tag = "t" + Guid.NewGuid().ToString("N")[..14];
        var root = await alice.CreatePostAsync($"#{tag.ToUpperInvariant()}");
        var reply = await bob.ReplyAsync(root.Id, $"#{tag}");

        var found = await Search(alice, $"#{tag.ToUpperInvariant()}");

        Assert.Equal(new[] { reply.Id, root.Id }, found.Select(p => p.Id));
    }

    [Fact]
    public async Task TheHashSearchPagesCorrectly()
    {
        var alice = await api.RegisterAsync("alice");
        var tag = "t" + Guid.NewGuid().ToString("N")[..14];
        var first = await alice.CreatePostAsync($"one #{tag}");
        var second = await alice.CreatePostAsync($"two #{tag}");

        var firstPage = await SearchPage(alice, $"#{tag}", take: 1);
        Assert.Equal(new[] { second.Id }, firstPage.Items.Select(p => p.Id));
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await SearchPage(alice, $"#{tag}", firstPage.NextCursor, take: 1);
        Assert.Equal(new[] { first.Id }, secondPage.Items.Select(p => p.Id));
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task ABackslashInTheQuery_IsTakenLiterally()
    {
        var alice = await api.RegisterAsync("alice");
        var word = Token();
        var post = await alice.CreatePostAsync($@"a path: C:\{word}\folder");

        var found = Assert.Single(await Search(alice, $@"C:\{word}\folder"));

        Assert.Equal(post.Id, found.Id);
    }

    [Fact]
    public async Task AHashFollowedByOnlyDigits_IsNotATag_SoItSearchesLiterally()
    {
        // A hashtag needs at least one letter, same rule as posting one (HashtagParser.HasLetter)
        var alice = await api.RegisterAsync("alice");
        var digits = string.Concat(Enumerable.Range(0, 16).Select(_ => Random.Shared.Next(10)));
        var post = await alice.CreatePostAsync($"see #{digits} for details");

        var found = Assert.Single(await Search(alice, $"#{digits}"));

        Assert.Equal(post.Id, found.Id);
    }

    [Fact]
    public async Task AHashFollowedBySomethingThatIsNotAWholeTag_SearchesLiterally()
    {
        // A space breaks the "whole tag" shape, so the query (hash included) is matched as ordinary text instead
        var alice = await api.RegisterAsync("alice");
        var word = Token();
        var query = $"#{word} extra";
        var post = await alice.CreatePostAsync($"see {query} written out");

        var found = Assert.Single(await Search(alice, query));

        Assert.Equal(post.Id, found.Id);
    }

    // ---- paging ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task PagesCorrectly_EvenWhenAPostIsDeletedBetweenPages()
    {
        var alice = await api.RegisterAsync("alice");
        var word = Token();
        var posts = new List<PostResponse>();
        for (var i = 0; i < 5; i++)
            posts.Add(await alice.CreatePostAsync($"post {i} {word}"));

        var firstPage = await SearchPage(alice, word, take: 2);
        Assert.Equal(new[] { posts[4].Id, posts[3].Id }, firstPage.Items.Select(p => p.Id));
        Assert.NotNull(firstPage.NextCursor);

        await (await alice.DeleteAsync($"/api/posts/{posts[2].Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        var secondPage = await SearchPage(alice, word, firstPage.NextCursor, take: 2);
        Assert.Equal(new[] { posts[1].Id, posts[0].Id }, secondPage.Items.Select(p => p.Id));
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task ANewMatchingPostWhilePagingDoesNotRepeatOrSkipAnything()
    {
        var alice = await api.RegisterAsync("alice");
        var word = Token();
        var first = await alice.CreatePostAsync($"first {word}");
        var second = await alice.CreatePostAsync($"second {word}");

        var firstPage = await SearchPage(alice, word, take: 1);
        Assert.Equal(new[] { second.Id }, firstPage.Items.Select(p => p.Id));
        Assert.NotNull(firstPage.NextCursor);

        var third = await alice.CreatePostAsync($"third {word}"); // arrives after the first page was read

        var secondPage = await SearchPage(alice, word, firstPage.NextCursor, take: 50);
        Assert.Equal(new[] { first.Id }, secondPage.Items.Select(p => p.Id)); // `third` is not repeated, `first` is not skipped
    }

    // ---- what is refused --------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyOrMissingQuery_IsRefused(string query)
    {
        var alice = await api.RegisterAsync("alice");

        await (await alice.GetAsync(SearchPath(query))).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await alice.GetAsync("/api/posts/search")).ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AQueryOver100Characters_IsRefused_100IsFine()
    {
        var alice = await api.RegisterAsync("alice");

        await (await alice.GetAsync(SearchPath(new string('a', 101)))).ShouldBeAsync(HttpStatusCode.BadRequest);

        var response = await alice.GetAsync(SearchPath(new string('a', 100)));
        await response.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ABadCursor_Is400()
    {
        var alice = await api.RegisterAsync("alice");

        await (await alice.GetAsync(SearchPath("anything", cursor: "not-a-cursor"))).ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignedOutVisitors_CannotSearchPosts()
    {
        var response = await api.Anonymous.GetAsync(SearchPath("anything"));

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PageSizeIsBounded()
    {
        var alice = await api.RegisterAsync("alice");
        var word = Token();
        await alice.CreatePostAsync(word);
        await alice.CreatePostAsync(word);

        var minimum = await SearchPage(alice, word, take: 0);
        Assert.Single(minimum.Items);

        var huge = await SearchPage(alice, word, take: 100000);
        Assert.True(huge.Items.Count <= 50);
    }
}
