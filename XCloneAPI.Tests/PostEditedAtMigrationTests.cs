using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XCloneAPI.Data;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>The migration that adds posts.edited_at: old posts count as never edited, and it can be taken off again.</summary>
public sealed class PostEditedAtMigrationTests : IAsyncLifetime
{
    private const string MigrationBefore = "AddPostMentions";

    private readonly TestDatabase _database = new();

    public Task InitializeAsync() => _database.CreateAsync();

    public Task DisposeAsync() => _database.DropAsync();

    private static async Task<int> InsertOldPostAsync(AppDbContext db)
    {
        var userIds = await db.Database.SqlQuery<int>($@"
            INSERT INTO users (username, email, password_hash, display_name, bio, avatar_url, created_at, updated_at)
            VALUES ('old_author', 'old_author@old.example.test', 'x', 'Old Author', '', '', now(), now())
            RETURNING id AS ""Value""").ToListAsync();
        var postIds = await db.Database.SqlQuery<int>($@"
            INSERT INTO posts (user_id, content, media_urls, likes_count, retweets_count, replies_count, created_at, updated_at)
            VALUES ({userIds.Single()}, 'written long ago', ARRAY[]::text[], 0, 0, 0, now(), now())
            RETURNING id AS ""Value""").ToListAsync();
        return postIds.Single();
    }

    private static Task<int> ColumnCountAsync(AppDbContext db) =>
        db.Database.SqlQuery<int>($@"
            SELECT count(*)::int AS ""Value"" FROM information_schema.columns
            WHERE table_name = 'posts' AND column_name = 'edited_at'").SingleAsync();

    [Fact]
    public async Task PostsFromBeforeTheColumn_ArePresentedAsNeverEdited()
    {
        await using var db = _database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBefore);
        Assert.Equal(0, await ColumnCountAsync(db));
        var oldPost = await InsertOldPostAsync(db);

        await migrator.MigrateAsync();

        Assert.Equal(1, await ColumnCountAsync(db));
        var post = await db.Posts.AsNoTracking().SingleAsync(p => p.Id == oldPost);
        Assert.Equal("written long ago", post.Content);
        Assert.Null(post.EditedAt);
    }

    [Fact]
    public async Task TheMigrationCanBeTakenOff_EvenWhenTheColumnIsAlreadyGone_AndPutOnAgain()
    {
        await using var db = _database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await db.Database.MigrateAsync();

        await db.Database.ExecuteSqlRawAsync("ALTER TABLE posts DROP COLUMN edited_at");   // as in a database built by hand
        await migrator.MigrateAsync(MigrationBefore);

        Assert.Equal(0, await ColumnCountAsync(db));
        Assert.DoesNotContain(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("AddPostEditedAt"));

        await migrator.MigrateAsync();

        Assert.Equal(1, await ColumnCountAsync(db));
    }
}
