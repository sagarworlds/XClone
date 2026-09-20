using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>
/// Checks the API against what the Angular app actually calls and expects. The frontend source is read directly,
/// so renaming a route or a JSON field on either side without the other breaks a test instead of the running app.
/// </summary>
[Collection(ApiCollection.Name)]
public partial class ContractTests(ApiFixture api)
{
    // this.http.post<...>(`${this.baseUrl}/likes/toggle/${postId}`, ...) -> ("post", "/likes/toggle/${postId}")
    [GeneratedRegex(@"\.(?<method>get|post|put|delete)<[^`(]*?>\(\s*`(?<url>[^`]+)`", RegexOptions.IgnoreCase)]
    private static partial Regex HttpCall();

    [GeneratedRegex(@"export interface (?<name>\w+) \{(?<body>.*?)\n\}", RegexOptions.Singleline)]
    private static partial Regex TypeScriptInterface();

    [GeneratedRegex(@"^\s*(?<prop>\w+)\??:", RegexOptions.Multiline)]
    private static partial Regex InterfaceProperty();

    // ---- routes ---------------------------------------------------------------------------------------------

    [FactWithFrontend]
    public async Task EveryUrlTheFrontendCalls_ExistsOnTheApi()
    {
        var calls = HttpCall().Matches(Frontend.Read("services/api.service.ts"))
            .Select(m => (
                Method: new HttpMethod(m.Groups["method"].Value.ToUpperInvariant()),
                Path: Regex.Replace(m.Groups["url"].Value.Replace("${this.baseUrl}", "/api"), @"\$\{[^}]+\}", "1")))
            .Distinct()
            .ToList();

        // Guards the extraction itself: if the regex silently stopped matching, this test would pass vacuously.
        Assert.True(calls.Count >= 20, $"expected to find the frontend's API calls but only found {calls.Count}");

        var problems = new List<string>();
        foreach (var (method, path) in calls)
        {
            var request = new HttpRequestMessage(method, path);
            if (method == HttpMethod.Post || method == HttpMethod.Put)
                request.Content = JsonContent.Create(new { });

            var response = await api.Anonymous.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
                problems.Add($"{method} {path}: the route exists but not for this HTTP method (405)");
            else if (response.StatusCode == HttpStatusCode.NotFound && body.Contains("Endpoint not found"))
                problems.Add($"{method} {path}: no such endpoint on the API");
            else if ((int)response.StatusCode >= 500)
                problems.Add($"{method} {path}: server error {(int)response.StatusCode}");
        }

        Assert.True(problems.Count == 0, "The frontend calls API routes that don't work:\n  " + string.Join("\n  ", problems));
    }

    [Fact]
    public async Task UnknownRoutes_GetAJson404()
    {
        var response = await api.Anonymous.GetAsync("/api/definitely/not/a/route");

        await response.ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Equal("Endpoint not found", await response.ReadMessageAsync());
    }

    // ---- JSON shapes ----------------------------------------------------------------------------------------

    private static HashSet<string> FrontendTypeProperties(string typeName)
    {
        var interfaces = TypeScriptInterface().Matches(Frontend.Read("models/types.ts"))
            .ToDictionary(m => m.Groups["name"].Value, m => m.Groups["body"].Value);
        Assert.True(interfaces.ContainsKey(typeName), $"types.ts no longer declares '{typeName}'");

        return InterfaceProperty().Matches(interfaces[typeName]).Select(m => m.Groups["prop"].Value).ToHashSet();
    }

    private static HashSet<string> JsonKeys(JsonElement element) => element.EnumerateObject().Select(p => p.Name).ToHashSet();

    [FactWithFrontend]
    public async Task AuthResponse_HasEveryFieldTheFrontendTypeDeclares()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var response = await api.Anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"shape_{suffix}",
            email = $"shape_{suffix}@example.test",
            password = "Passw0rd!x",
        });
        await response.ShouldBeAsync(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Empty(FrontendTypeProperties("AuthResponse").Except(JsonKeys(json.RootElement)));
    }

    [FactWithFrontend]
    public async Task UserResponse_HasEveryFieldTheFrontendTypeDeclares()
    {
        var me = await api.RegisterAsync("shape");

        using var json = JsonDocument.Parse(await (await me.GetAsync("/api/users/profile")).Content.ReadAsStringAsync());

        Assert.Empty(FrontendTypeProperties("User").Except(JsonKeys(json.RootElement)));
    }

    [FactWithFrontend]
    public async Task PostResponse_HasEveryFieldTheFrontendTypeDeclares_IncludingNestedUsers()
    {
        var author = await api.RegisterAsync("author");
        var reposter = await api.RegisterAsync("reposter");
        await reposter.FollowAsync(author);
        var post = await author.CreatePostAsync("shape check");
        await reposter.ReplyAsync(post.Id, "a reply");
        await author.FollowAsync(reposter);
        await reposter.ToggleRetweetAsync(post.Id);   // so the feed has an entry with retweetedBy filled in

        var feed = JsonDocument.Parse(await (await author.GetAsync("/api/posts/feed")).Content.ReadAsStringAsync());
        var replies = JsonDocument.Parse(await (await author.GetAsync($"/api/posts/{post.Id}/replies")).Content.ReadAsStringAsync());

        var postProperties = FrontendTypeProperties("Post");
        var userProperties = FrontendTypeProperties("User");

        foreach (var entry in feed.RootElement.EnumerateArray().Concat(replies.RootElement.EnumerateArray()))
        {
            Assert.Empty(postProperties.Except(JsonKeys(entry)));
            Assert.Empty(userProperties.Except(JsonKeys(entry.GetProperty("user"))));

            if (entry.GetProperty("retweetedBy").ValueKind == JsonValueKind.Object)
                Assert.Empty(userProperties.Except(JsonKeys(entry.GetProperty("retweetedBy"))));
        }

        Assert.Contains(feed.RootElement.EnumerateArray(), e => e.GetProperty("retweetedBy").ValueKind == JsonValueKind.Object);
    }

    // ---- CORS -----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("http://localhost:4200")]
    [InlineData("http://localhost:4300")]
    public async Task Cors_AllowsTheAngularDevServer_ToSendAuthenticatedJsonRequests(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/posts");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        var response = await api.Anonymous.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, $"preflight failed with {(int)response.StatusCode}");
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("authorization", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cors_DoesNotAllowOtherWebsites()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/posts");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await api.Anonymous.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
