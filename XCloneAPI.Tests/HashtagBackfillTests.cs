using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XCloneAPI.Services;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>
/// Posts written before hashtags existed get their tags from the migration (in SQL). This puts such posts into a
/// database as it was just before that migration, runs the migration, and compares with what the parser says.
/// </summary>
public sealed class HashtagBackfillTests : IAsyncLifetime
{
    private const string MigrationBefore = "AddFollowKeysetIndexes";

    private readonly TestDatabase _database = new();

    public Task InitializeAsync() => _database.CreateAsync();

    public Task DisposeAsync() => _database.DropAsync();

    private static async Task<int> InsertUserAsync(Data.AppDbContext db)
    {
        var ids = await db.Database.SqlQuery<int>($@"
            INSERT INTO users (username, email, password_hash, display_name, bio, avatar_url, created_at, updated_at)
            VALUES ('old_user', 'old@example.test', 'x', 'Old', '', '', now(), now())
            RETURNING id AS ""Value""").ToListAsync();
        return ids.Single();
    }

    private static async Task<int> InsertPostAsync(Data.AppDbContext db, int userId, string content, int? parentId = null)
    {
        var ids = await db.Database.SqlQuery<int>($@"
            INSERT INTO posts (user_id, content, media_urls, likes_count, retweets_count, replies_count, parent_post_id, created_at, updated_at)
            VALUES ({userId}, {content}, ARRAY[]::text[], 0, 0, 0, {parentId}, now(), now())
            RETURNING id AS ""Value""").ToListAsync();
        return ids.Single();
    }

    private static IEnumerable<string> SharedTexts()
    {
        using var document = JsonDocument.Parse(Frontend.Read("../testing/text-entities.json"));
        return document.RootElement.GetProperty("hashtags").EnumerateArray().Select(c => c.GetProperty("text").GetString()!).ToList();
    }

    // Scripts that build words out of combining marks (such as Devanagari) are not letters to PostgreSQL's [[:alnum:]]
    // in every locale, so the backfill may split those words differently from the parser. Everything else has to agree.
    private static bool NeedsCombiningMarks(string text) =>
        text.Any(c => char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.NonSpacingMark
            or System.Globalization.UnicodeCategory.SpacingCombiningMark);

    [FactWithFrontend]
    public async Task OldPosts_GetTheTagsTheParserWouldHaveGivenThem()
    {
        await using var db = _database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBefore);   // takes the hashtags migration back off
        Assert.Empty((await db.Database.GetAppliedMigrationsAsync()).Where(m => m.EndsWith("AddPostHashtags")));

        var userId = await InsertUserAsync(db);
        var expected = new Dictionary<int, (string Text, List<string> Tags)>();
        var texts = SharedTexts()
            .Where(t => !NeedsCombiningMarks(t))
            .Concat(new[]
            {
                string.Join(" ", Enumerable.Range(1, 12).Select(i => $"#many{i}")),    // only the first ten
                $"#{new string('a', 50)} and #{new string('b', 51)} too long",         // fifty is a tag, fifty-one is not
                "#Repeat #repeat #REPEAT and #other",                                  // once each
            })
            .ToList();
        foreach (var text in texts)
            expected[await InsertPostAsync(db, userId, text)] = (text, HashtagParser.Parse(text));
        var parent = await InsertPostAsync(db, userId, "a parent");
        var reply = await InsertPostAsync(db, userId, "a reply #backfilled", parent);   // replies are covered too
        expected[reply] = ("a reply #backfilled", new List<string> { "backfilled" });

        await migrator.MigrateAsync();   // the hashtags migration, with its backfill

        var rows = await db.PostHashtags.AsNoTracking().ToListAsync();
        var actual = rows.GroupBy(r => r.PostId).ToDictionary(g => g.Key, g => g.Select(r => r.Tag).ToList());
        var problems = expected
            .Where(e => !e.Value.Tags.OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(
                actual.GetValueOrDefault(e.Key, new List<string>()).OrderBy(t => t, StringComparer.Ordinal)))
            .Select(e => $"\"{e.Value.Text.Replace("\n", "\\n")}\": parser says [{string.Join(", ", e.Value.Tags)}], the migration wrote [{string.Join(", ", actual.GetValueOrDefault(e.Key, new List<string>()))}]")
            .ToList();

        Assert.True(problems.Count == 0, "The backfill and the parser disagree:\n  " + string.Join("\n  ", problems));
        Assert.Empty(actual.Keys.Except(expected.Keys));   // nothing for the post that has no tags
        Assert.True(expected.Values.Count(e => e.Tags.Count > 0) >= 20, "expected plenty of tagged posts to compare");
    }

    [Fact]
    public async Task TheMigrationCanBeTakenBackOffAndPutOnAgain()
    {
        await using var db = _database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        var userId = await InsertUserAsync(db);
        await InsertPostAsync(db, userId, "#first");

        await migrator.MigrateAsync(MigrationBefore);
        Assert.False(db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM information_schema.tables WHERE table_name = 'post_hashtags'").ToList().Single() > 0);

        await migrator.MigrateAsync();
        Assert.Equal(new[] { "first" }, await db.PostHashtags.Select(h => h.Tag).ToListAsync());   // written again by the backfill
    }
}
