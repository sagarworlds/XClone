using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using XCloneAPI.Data;

namespace XCloneAPI.Tests.Infrastructure;

/// <summary>
/// A throwaway PostgreSQL database that exists only for one test run.
/// It is created from the real EF migrations (so a broken migration fails the tests) and dropped afterwards.
/// The database is always named xclone_it_&lt;random&gt;, so it can never collide with your development database.
/// </summary>
public sealed class TestDatabase
{
    public string ConnectionString { get; private set; } = "";

    public async Task CreateAsync()
    {
        var server = new NpgsqlConnectionStringBuilder(ResolveServerConnectionString())
        {
            Database = $"xclone_it_{Guid.NewGuid():N}"
        };
        ConnectionString = server.ConnectionString;

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DropAsync()
    {
        if (ConnectionString == "") return;

        NpgsqlConnection.ClearAllPools();
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>
    /// Which PostgreSQL server to use, in order:
    /// 1. the XCLONE_TEST_CONNECTION environment variable (used by CI),
    /// 2. the API project's user-secrets connection string (used on a developer machine).
    /// Only host, port and credentials are taken from it; the database name is always replaced.
    /// </summary>
    private static string ResolveServerConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("XCLONE_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        var secrets = new ConfigurationBuilder().AddUserSecrets(typeof(Program).Assembly).Build();
        var fromSecrets = secrets.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromSecrets) && !fromSecrets.Contains("CHANGE-ME", StringComparison.OrdinalIgnoreCase))
            return fromSecrets;

        throw new InvalidOperationException(
            "The integration tests need a PostgreSQL server. Either run the API once with user-secrets configured " +
            "(dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"Host=localhost;...\" in XCloneAPI), " +
            "or set the XCLONE_TEST_CONNECTION environment variable, e.g. " +
            "\"Host=localhost;Port=5432;Username=postgres;Password=<password>\". " +
            "The tests create and drop their own database (xclone_it_*) and never touch your development data.");
    }
}
