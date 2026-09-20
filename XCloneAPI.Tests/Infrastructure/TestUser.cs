using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using XCloneAPI.DTOs;

namespace XCloneAPI.Tests.Infrastructure;

/// <summary>A registered user together with an HTTP client that is signed in as them.</summary>
public sealed class TestUser
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public int Id { get; private init; }
    public string Username { get; private init; } = "";
    public string Email { get; private init; } = "";
    public string Password { get; private init; } = "";
    public string Token { get; private init; } = "";
    public HttpClient Client { get; private init; } = null!;

    public static async Task<TestUser> RegisterAsync(ApiFactory factory, string name, string password)
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var username = $"{name}_{unique}";
        var email = $"{username}@example.test";

        using var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            username,
            email,
            password,
            displayName = $"{name} {unique}",
        });
        await response.ShouldBeAsync(HttpStatusCode.OK);
        var auth = await response.ReadAsync<AuthResponse>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        return new TestUser
        {
            Id = auth.Id,
            Username = username,
            Email = email,
            Password = password,
            Token = auth.Token,
            Client = client,
        };
    }

    // ---- raw requests -------------------------------------------------------------------------------------

    public Task<HttpResponseMessage> GetAsync(string path) => Client.GetAsync(path);
    public Task<HttpResponseMessage> PostAsync(string path, object? body = null) => Client.PostAsJsonAsync(path, body ?? new { });
    public Task<HttpResponseMessage> PutAsync(string path, object body) => Client.PutAsJsonAsync(path, body);
    public Task<HttpResponseMessage> DeleteAsync(string path) => Client.DeleteAsync(path);

    /// <summary>GET, assert 200, and deserialize.</summary>
    public async Task<T> GetAsync<T>(string path)
    {
        var response = await GetAsync(path);
        await response.ShouldBeAsync(HttpStatusCode.OK);
        return await response.ReadAsync<T>();
    }

    // ---- domain helpers (each asserts success, so a test reads as a story) ---------------------------------

    public async Task<PostResponse> CreatePostAsync(string content)
    {
        var response = await PostAsync("/api/posts", new { content });
        await response.ShouldBeAsync(HttpStatusCode.Created);
        return await response.ReadAsync<PostResponse>();
    }

    public async Task<PostResponse> ReplyAsync(int postId, string content)
    {
        var response = await PostAsync($"/api/posts/{postId}/replies", new { content });
        await response.ShouldBeAsync(HttpStatusCode.Created);
        return await response.ReadAsync<PostResponse>();
    }

    public Task<PostResponse> GetPostAsync(int postId) => GetAsync<PostResponse>($"/api/posts/{postId}");

    public async Task FollowAsync(TestUser other)
    {
        var response = await PostAsync($"/api/follows/toggle/{other.Id}");
        await response.ShouldBeAsync(HttpStatusCode.OK);
        Assert.True((await response.ReadAsync<Dictionary<string, bool>>())["followed"], "expected the follow toggle to turn following ON");
    }

    public async Task<bool> ToggleLikeAsync(int postId)
    {
        var response = await PostAsync($"/api/likes/toggle/{postId}");
        await response.ShouldBeAsync(HttpStatusCode.OK);
        return (await response.ReadAsync<Dictionary<string, bool>>())["liked"];
    }

    public async Task<bool> ToggleRetweetAsync(int postId)
    {
        var response = await PostAsync($"/api/retweets/toggle/{postId}");
        await response.ShouldBeAsync(HttpStatusCode.OK);
        return (await response.ReadAsync<Dictionary<string, bool>>())["retweeted"];
    }

    public Task<List<PostResponse>> FeedAsync(int skip = 0, int take = 50) =>
        GetAsync<List<PostResponse>>($"/api/posts/feed?skip={skip}&take={take}");
}

public static class HttpResponseExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(TestUser.Json);
        return value ?? throw new InvalidOperationException("Response body was empty or null.");
    }

    /// <summary>Asserts the status code; on failure the response body is part of the message.</summary>
    public static async Task ShouldBeAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode == expected) return;
        var body = await response.Content.ReadAsStringAsync();
        Assert.Fail($"Expected HTTP {(int)expected} {expected} but got {(int)response.StatusCode} {response.StatusCode} from " +
                    $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}. Body: {body}");
    }

    /// <summary>Reads the "message" field the API uses for error responses.</summary>
    public static async Task<string?> ReadMessageAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(TestUser.Json);
        return body != null && body.TryGetValue("message", out var message) ? message.GetString() : null;
    }
}
