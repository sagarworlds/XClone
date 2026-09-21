using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XCloneAPI.Data;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>
/// Posts written before mentions existed get their mention links from the migration (in SQL). This puts such posts
/// and accounts into a database as it was just before that migration, runs the migration, and checks who is linked.
/// </summary>
public sealed class MentionBackfillTests : IAsyncLifetime
{
    private const string MigrationBefore = "AddPostHashtags";

    private readonly TestDatabase _database = new();

    public Task InitializeAsync() => _database.CreateAsync();

    public Task DisposeAsync() => _database.DropAsync();

    private static async Task<int> InsertUserAsync(AppDbContext db, string username)
    {
        var email = $"{username}@old.example.test";
        var ids = await db.Database.SqlQuery<int>($@"
            INSERT INTO users (username, email, password_hash, display_name, bio, avatar_url, created_at, updated_at)
            VALUES ({username}, {email}, 'x', {username}, '', '', now(), now())
            RETURNING id AS ""Value""").ToListAsync();
        return ids.Single();
    }

    private static async Task<int> InsertPostAsync(AppDbContext db, int userId, string content)
    {
        var ids = await db.Database.SqlQuery<int>($@"
            INSERT INTO posts (user_id, content, media_urls, likes_count, retweets_count, replies_count, created_at, updated_at)
            VALUES ({userId}, {content}, ARRAY[]::text[], 0, 0, 0, now(), now())
            RETURNING id AS ""Value""").ToListAsync();
        return ids.Single();
    }

    private static List<(string Text, string[] Names)> SharedCases()
    {
        using var document = JsonDocument.Parse(Frontend.Read("../testing/text-entities.json"));
        return document.RootElement.GetProperty("mentions").EnumerateArray()
            .Select(c => (c.GetProperty("text").GetString()!, c.GetProperty("names").EnumerateArray().Select(t => t.GetString()!).ToArray()))
            .ToList();
    }

    private static async Task<Dictionary<int, List<int>>> LinksAsync(AppDbContext db) =>
        (await db.PostMentions.AsNoTracking().ToListAsync())
            .GroupBy(m => m.PostId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.UserId).OrderBy(x => x).ToList());

    [FactWithFrontend]
    public async Task OldPosts_LinkTheSameAccountsTheParserWouldHaveFound()
    {
        await using var db = _database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBefore);   // takes the mentions migration back off
        Assert.Empty((await db.Database.GetAppliedMigrationsAsync()).Where(m => m.EndsWith("AddPostMentions")));

        var cases = SharedCases();
        var author = await InsertUserAsync(db, "the_author");
        var accounts = new Dictionary<string, int>();   // lower-case name -> account
        foreach (var name in cases.SelectMany(c => c.Names).Select(n => n.ToLowerInvariant()).Distinct())
            accounts[name] = await InsertUserAsync(db, name);

        var expected = new Dictionary<int, (string Text, List<int> Users)>();
        foreach (var (text, names) in cases)
            expected[await InsertPostAsync(db, author, text)] = (text, names.Select(n => accounts[n.ToLowerInvariant()]).Distinct().OrderBy(x => x).ToList());

        await migrator.MigrateAsync();   // the mentions migration, with its backfill

        var actual = await LinksAsync(db);
        var problems = expected
            .Where(e => !e.Value.Users.SequenceEqual(actual.GetValueOrDefault(e.Key, new List<int>())))
            .Select(e => $"\"{e.Value.Text.Replace("\n", "\\n")}\": expected accounts [{string.Join(", ", e.Value.Users)}] but the migration linked [{string.Join(", ", actual.GetValueOrDefault(e.Key, new List<int>()))}]")
            .ToList();

        Assert.True(problems.Count == 0, "The backfill and the parser disagree:\n  " + string.Join("\n  ", problems));
        Assert.Empty(actual.Keys.Except(expected.Keys));
        Assert.True(expected.Values.Count(e => e.Users.Count > 0) >= 25, "expected plenty of posts with mentions to compare");
    }

    [Fact]
    public async Task OnlyTheFirstTenNamesCount_UnknownOnesIncluded()
    {
        await using var db = _database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBefore);
        var author = await InsertUserAsync(db, "the_author");
        var people = new List<int>();
        for (var i = 1; i <= 12; i++) people.Add(await InsertUserAsync(db, $"person{i:00}"));
        var twelve = await InsertPostAsync(db, author, string.Join(" ", Enumerable.Range(1, 12).Select(i => $"@person{i:00}")));
        var ghostsFirst = await InsertPostAsync(db, author, string.Join(" ", Enumerable.Range(1, 10).Select(i => $"@ghost{i:00}")) + " @person01");

        await migrator.MigrateAsync();

        var links = await LinksAsync(db);
        Assert.Equal(people.Take(10).OrderBy(x => x), links[twelve]);
        Assert.False(links.ContainsKey(ghostsFirst));   // the eleventh name is not counted, however many before it are unknown
    }

    [Fact]
    public async Task WhenTwoAccountsDifferOnlyByCase_TheExactSpellingWins_ElseTheOldest()
    {
        await using var db = _database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBefore);
        var author = await InsertUserAsync(db, "the_author");
        var older = await InsertUserAsync(db, "DupeAcc");
        var newer = await InsertUserAsync(db, "dupeacc");
        var exactNewer = await InsertPostAsync(db, author, "@dupeacc");
        var exactOlder = await InsertPostAsync(db, author, "@DupeAcc");
        var neither = await InsertPostAsync(db, author, "@DUPEACC");

        await migrator.MigrateAsync();

        var links = await LinksAsync(db);
        Assert.Equal(new[] { newer }, links[exactNewer]);
        Assert.Equal(new[] { older }, links[exactOlder]);
        Assert.Equal(new[] { older }, links[neither]);
    }

    [Fact]
    public async Task ExistingAccounts_AreNotLinkedWhenTheTextIsNotAMentionOfThem()
    {
        await using var db = _database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBefore);
        var author = await InsertUserAsync(db, "the_author");
        var bobe = await InsertUserAsync(db, "bobe");
        await InsertUserAsync(db, "ab");   // accounts this short exist from before usernames had a minimum
        var plain = await InsertPostAsync(db, author, "@bobe");
        var accented = await InsertPostAsync(db, author, "@bobe\u0301");   // "bobé": the accent is part of the word
        var tooShort = await InsertPostAsync(db, author, "hello @ab");

        await migrator.MigrateAsync();

        var links = await LinksAsync(db);
        Assert.Equal(new[] { bobe }, links[plain]);   // the same account is linked when written properly
        Assert.False(links.ContainsKey(accented));
        Assert.False(links.ContainsKey(tooShort));
    }

    [Fact]
    public async Task NobodyIsNotifiedAboutOldPosts()
    {
        await using var db = _database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBefore);
        var author = await InsertUserAsync(db, "the_author");
        await InsertUserAsync(db, "bystander");
        await InsertPostAsync(db, author, "hello @bystander");

        await migrator.MigrateAsync();

        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task TheMigrationCanBeTakenBackOffAndPutOnAgain()
    {
        await using var db = _database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        var author = await InsertUserAsync(db, "the_author");
        await InsertUserAsync(db, "friend");
        await InsertPostAsync(db, author, "@friend");

        await migrator.MigrateAsync(MigrationBefore);
        await migrator.MigrateAsync();

        Assert.Single(await db.PostMentions.ToListAsync());
    }
}
