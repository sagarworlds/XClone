using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>The API must refuse to start with placeholder or missing secrets, so a fresh clone can never run on a public key.</summary>
[Collection(ApiCollection.Name)]
public class StartupConfigTests(ApiFixture api)
{
    private static string AllMessages(Exception? ex)
    {
        var messages = new List<string>();
        for (; ex != null; ex = ex.InnerException) messages.Add(ex.Message);
        return string.Join(" | ", messages);
    }

    private string StartupError(Dictionary<string, string?> overrides)
    {
        using var factory = ApiFactory.ForDatabase(api.ConnectionString, overrides);
        var error = Record.Exception(() => factory.CreateClient());
        Assert.True(error != null, "the API started even though its configuration was invalid");
        return AllMessages(error);
    }

    [Theory]
    [InlineData("CHANGE-ME-set-with-dotnet-user-secrets")]                          // exactly what appsettings.json ships with
    [InlineData("CHANGE-ME-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]   // a long "fake" key that would pass a length check
    [InlineData("too-short")]
    [InlineData("")]
    [InlineData("   ")]
    public void RefusesToStart_WithAMissingShortOrPlaceholderJwtKey(string key)
    {
        Assert.Contains("Jwt:Key", StartupError(new() { ["Jwt:Key"] = key }));
    }

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=x_clone_db;Username=postgres;Password=CHANGE-ME")]
    [InlineData("")]
    public void RefusesToStart_WithAMissingOrPlaceholderConnectionString(string connectionString)
    {
        Assert.Contains("ConnectionStrings:DefaultConnection", StartupError(new() { ["ConnectionStrings:DefaultConnection"] = connectionString }));
    }

    [Fact]
    public async Task StartsNormally_WithRealValues()
    {
        await using var factory = ApiFactory.ForDatabase(api.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/users/search?query=anything");

        Assert.True(response.IsSuccessStatusCode);
    }
}
