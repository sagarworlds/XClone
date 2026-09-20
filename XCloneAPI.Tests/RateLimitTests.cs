using System.Net;
using System.Net.Http.Json;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>Uses its own API host with a tiny limit, so the shared host's high limit doesn't hide this behaviour.</summary>
[Collection(ApiCollection.Name)]
public class RateLimitTests(ApiFixture api)
{
    private ApiFactory LimitedFactory(int permits) =>
        ApiFactory.ForDatabase(api.ConnectionString, new() { ["RateLimiting:AuthPermitLimit"] = permits.ToString() });

    private static Task<HttpResponseMessage> BadLogin(HttpClient client, string? origin = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { usernameOrEmail = "nobody_at_all", password = "wrong-pass" }),
        };
        if (origin != null) request.Headers.Add("Origin", origin);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task LoginAttempts_AreLimited_AfterTheAllowedNumber()
    {
        await using var factory = LimitedFactory(3);
        using var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
            await (await BadLogin(client)).ShouldBeAsync(HttpStatusCode.Unauthorized);   // allowed, and rejected as wrong credentials

        var blocked = await BadLogin(client);
        await blocked.ShouldBeAsync(HttpStatusCode.TooManyRequests);
        Assert.Contains("Too many attempts", await blocked.ReadMessageAsync());
    }

    [Fact]
    public async Task Registration_SharesTheSameLimit()
    {
        await using var factory = LimitedFactory(2);
        using var client = factory.CreateClient();

        async Task<HttpStatusCode> Register() => (await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"rl_{Guid.NewGuid():N}"[..15],
            email = $"{Guid.NewGuid():N}@example.test",
            password = "Passw0rd!x",
        })).StatusCode;

        Assert.Equal(HttpStatusCode.OK, await Register());
        Assert.Equal(HttpStatusCode.OK, await Register());
        Assert.Equal(HttpStatusCode.TooManyRequests, await Register());
    }

    [Fact]
    public async Task TheLimitOnlyAppliesToAuthEndpoints()
    {
        await using var factory = LimitedFactory(1);
        using var client = factory.CreateClient();
        await BadLogin(client);
        await (await BadLogin(client)).ShouldBeAsync(HttpStatusCode.TooManyRequests);

        // Everything else keeps working while login is throttled.
        for (var i = 0; i < 5; i++)
            await (await client.GetAsync("/api/users/search?query=anything")).ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheBlockedResponse_StillCarriesCorsHeaders_SoTheBrowserCanShowTheMessage()
    {
        await using var factory = LimitedFactory(1);
        using var client = factory.CreateClient();
        await BadLogin(client, "http://localhost:4200");

        var blocked = await BadLogin(client, "http://localhost:4200");

        await blocked.ShouldBeAsync(HttpStatusCode.TooManyRequests);
        Assert.Equal("http://localhost:4200", Assert.Single(blocked.Headers.GetValues("Access-Control-Allow-Origin")));
    }
}
