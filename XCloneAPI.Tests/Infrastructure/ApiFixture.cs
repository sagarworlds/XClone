using Microsoft.Extensions.DependencyInjection;
using XCloneAPI.Data;

namespace XCloneAPI.Tests.Infrastructure;

/// <summary>One database and one API host shared by every test class (created once, dropped at the end).</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly TestDatabase _database = new();

    public ApiFactory Factory { get; private set; } = null!;
    public string ConnectionString => _database.ConnectionString;

    /// <summary>A client with no Authorization header.</summary>
    public HttpClient Anonymous { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.CreateAsync();
        Factory = ApiFactory.ForDatabase(_database.ConnectionString);
        Anonymous = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Anonymous.Dispose();
        await Factory.DisposeAsync();
        await _database.DropAsync();
    }

    /// <summary>Registers a fresh user with a unique name, so tests never interfere with each other.</summary>
    public Task<TestUser> RegisterAsync(string name = "user", string password = "Passw0rd!x") =>
        TestUser.RegisterAsync(Factory, name, password);

    /// <summary>Direct database access, for arranging or inspecting state the API doesn't expose.</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
