using Microsoft.EntityFrameworkCore;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

[Collection(ApiCollection.Name)]
public class MigrationTests(ApiFixture api)
{
    [Fact]
    public async Task EveryMigrationAppliesCleanly_ToAnEmptyDatabase()
    {
        // The fixture already ran them all against a brand-new database; this asserts nothing was left over.
        var (applied, pending) = await api.WithDbAsync(async db =>
            (await db.Database.GetAppliedMigrationsAsync(), await db.Database.GetPendingMigrationsAsync()));

        Assert.NotEmpty(applied);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task TheModelAndTheMigrationsAgree()
    {
        var pendingModelChanges = await api.WithDbAsync(db => Task.FromResult(db.Database.HasPendingModelChanges()));

        Assert.False(pendingModelChanges,
            "The EF model has changes that no migration covers. From the XCloneAPI folder run: dotnet ef migrations add <DescriptiveName>");
    }
}
