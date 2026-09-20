using System.Net;
using System.Net.Http.Json;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

[Collection(ApiCollection.Name)]
public class SecurityTests(ApiFixture api)
{
    private static HttpRequestMessage Request(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(new { content = "x", displayName = "x" });
        return request;
    }

    [Theory]
    [InlineData("GET", "/api/posts/feed")]
    [InlineData("POST", "/api/posts")]
    [InlineData("DELETE", "/api/posts/1")]
    [InlineData("POST", "/api/posts/1/replies")]
    [InlineData("POST", "/api/likes/toggle/1")]
    [InlineData("POST", "/api/retweets/toggle/1")]
    [InlineData("POST", "/api/follows/toggle/1")]
    [InlineData("GET", "/api/follows/is-following/1")]
    [InlineData("GET", "/api/users/profile")]
    [InlineData("GET", "/api/users/profile/someone")]
    [InlineData("GET", "/api/users/suggestions")]
    [InlineData("PUT", "/api/users/profile")]
    public async Task EndpointsThatActOnBehalfOfAUser_RequireLogin(string method, string path)
    {
        var response = await api.Anonymous.SendAsync(Request(method, path));

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/users/search?query=anything")]
    [InlineData("/api/users/1/followers")]
    [InlineData("/api/users/1/following")]
    [InlineData("/api/users/1")]
    [InlineData("/api/posts/1")]
    [InlineData("/api/posts/1/replies")]
    [InlineData("/api/posts/user/1")]
    [InlineData("/api/posts/user/1/replies")]
    [InlineData("/api/likes/post/1")]
    public async Task PublicReadEndpoints_DoNotDemandLogin(string path)
    {
        var response = await api.Anonymous.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True((int)response.StatusCode < 500, $"{path} returned {(int)response.StatusCode}");
    }

    [Fact]
    public async Task NoResponse_EverContainsAnotherUsersEmail_OrAnyPasswordHash()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        await alice.FollowAsync(bob);
        var post = await bob.CreatePostAsync("bob's post");
        await alice.ReplyAsync(post.Id, "alice replies");
        await bob.ToggleLikeAsync(post.Id);
        await bob.ToggleRetweetAsync(post.Id);
        await bob.FollowAsync(alice);

        // Everything alice (and an anonymous visitor) can read that involves bob.
        var paths = new[]
        {
            $"/api/users/profile/{bob.Username}", $"/api/users/{bob.Id}", $"/api/users/search?query={bob.Username}",
            $"/api/users/{bob.Id}/followers", $"/api/users/{bob.Id}/following", "/api/users/suggestions?take=20",
            "/api/posts/feed", $"/api/posts/{post.Id}", $"/api/posts/{post.Id}/replies", $"/api/posts/user/{bob.Id}",
            $"/api/posts/user/{bob.Id}/replies", $"/api/likes/post/{post.Id}",
        };

        var bodies = new List<string>();
        foreach (var path in paths)
        {
            bodies.Add(await (await alice.GetAsync(path)).Content.ReadAsStringAsync());
            bodies.Add(await (await api.Anonymous.GetAsync(path)).Content.ReadAsStringAsync());
        }

        var everything = string.Join("\n", bodies);
        Assert.DoesNotContain(bob.Email, everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AQAAAA", everything);   // the start of every stored password hash

        // ...while a user still gets their own email back.
        Assert.Contains(alice.Email, await (await alice.GetAsync("/api/users/profile")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OwnAuthResponses_NeverIncludeThePasswordHash()
    {
        var user = await api.RegisterAsync("plain");

        var login = await api.Anonymous.PostAsJsonAsync("/api/auth/login", new { usernameOrEmail = user.Username, password = user.Password });
        var body = await login.Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(user.Password, body);
    }

    [Fact]
    public async Task ATokenBelongsToOneUser_YouCannotActAsSomeoneElse()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("alice's post");

        // Bob's token can't delete Alice's post, and posts he creates are his own, whatever ids he sends.
        await (await bob.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NotFound);
        var created = await (await bob.PostAsync("/api/posts", new { content = "mine", userId = alice.Id })).ReadAsync<XCloneAPI.DTOs.PostResponse>();
        Assert.Equal(bob.Id, created.UserId);
    }

    [Fact]
    public async Task PostContent_IsReturnedAsPlainText_NotInterpreted()
    {
        var alice = await api.RegisterAsync("alice");
        const string script = "<script>alert('xss')</script> & \"quotes\"";

        var post = await alice.CreatePostAsync(script);

        Assert.Equal(script, (await alice.GetPostAsync(post.Id)).Content);   // stored and returned verbatim; escaping is the renderer's job (Angular escapes by default)
    }
}
