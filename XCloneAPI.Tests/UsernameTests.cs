using System.Net;
using System.Net.Http.Json;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>What a username may be. Mentions (@username) can only find names made of these characters.</summary>
[Collection(ApiCollection.Name)]
public class UsernameTests(ApiFixture api)
{
    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    private Task<HttpResponseMessage> Register(string username, string? email = null) =>
        api.Anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            username,
            email = email ?? $"{Unique()}@example.test",
            password = "Passw0rd!x",
            displayName = "Somebody",
        });

    [Theory]
    [InlineData("letters")]
    [InlineData("UPPER")]
    [InlineData("MiXeD")]
    [InlineData("digits123")]
    [InlineData("123456")]
    [InlineData("snake_case_name")]
    [InlineData("___")]
    [InlineData("_leading")]
    public async Task LettersDigitsAndUnderscores_AreFine(string stem)
    {
        var response = await Register(stem.Length < 4 ? stem : $"{stem}_{Unique()}");

        await response.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("hyphen-ated")]
    [InlineData("dot.ted")]
    [InlineData("at@sign")]
    [InlineData("hash#tag")]
    [InlineData("slash/es")]
    [InlineData("quo'te")]
    [InlineData("émile")]
    [InlineData("東京東京")]
    [InlineData("emoji😀😀")]
    [InlineData("trailing\n")]
    [InlineData("tab\there")]
    public async Task AnythingElse_IsRefused_WithAMessage(string username)
    {
        var response = await Register(username + "x" + Unique());

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Usernames can only contain letters, digits and underscores.", body);
    }

    [Fact]
    public async Task ANewlineAfterAGoodName_IsNotSlippedThrough()
    {
        var response = await Register("goodname_" + Unique() + "\n");

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnExistingNameInAnotherCase_IsTaken()
    {
        var taken = "Taken_" + Unique();
        await (await Register(taken)).ShouldBeAsync(HttpStatusCode.OK);

        foreach (var variant in new[] { taken.ToUpperInvariant(), taken.ToLowerInvariant(), taken })
        {
            var response = await Register(variant);

            await response.ShouldBeAsync(HttpStatusCode.BadRequest);
            Assert.Equal("Username already taken", await response.ReadMessageAsync());
        }
    }

    [Fact]
    public async Task TheLengthRulesStayAsTheyWere()
    {
        await (await Register("ab")).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await Register(new string('a', 51))).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await Register("abc" + Unique())).ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ALoginStillWorksWithTheNameInAnyCase_AsBefore()
    {
        var user = await api.RegisterAsync("casey");

        var response = await api.Anonymous.PostAsJsonAsync("/api/auth/login", new { usernameOrEmail = user.Username, password = user.Password });

        await response.ShouldBeAsync(HttpStatusCode.OK);
    }
}
