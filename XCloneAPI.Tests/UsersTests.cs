using System.Net;
using System.Net.Http.Json;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

[Collection(ApiCollection.Name)]
public class UsersTests(ApiFixture api)
{
    // ---- profiles -------------------------------------------------------------------------------------------

    [Fact]
    public async Task OwnProfile_IncludesTheEmail_ButOtherPeoplesProfilesDoNot()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");

        var own = await alice.GetAsync<UserResponse>("/api/users/profile");
        Assert.Equal(alice.Email, own.Email);

        var othersByUsername = await alice.GetAsync<UserResponse>($"/api/users/profile/{bob.Username}");
        Assert.Equal(bob.Id, othersByUsername.Id);
        Assert.True(string.IsNullOrEmpty(othersByUsername.Email), "another user's email must not be exposed");

        var anonymousById = await api.Anonymous.GetFromJsonAsync<UserResponse>($"/api/users/{bob.Id}", TestUser.Json);
        Assert.True(string.IsNullOrEmpty(anonymousById!.Email), "anonymous callers must not see emails");
    }

    [Fact]
    public async Task ProfileByUsername_404ForUnknownUsers_AndRequiresLogin()
    {
        var alice = await api.RegisterAsync("alice");

        await (await alice.GetAsync("/api/users/profile/nobody_by_that_name")).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await api.Anonymous.GetAsync($"/api/users/profile/{alice.Username}")).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await api.Anonymous.GetAsync("/api/users/profile")).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await api.Anonymous.GetAsync("/api/users/99999999")).ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Profile_ShowsFollowerCountsAndTheViewersFollowState()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");

        await bob.FollowAsync(alice);

        var seenByBob = await bob.GetAsync<UserResponse>($"/api/users/profile/{alice.Username}");
        Assert.Equal(1, seenByBob.FollowersCount);
        Assert.True(seenByBob.IsFollowed);

        var seenByAlice = await alice.GetAsync<UserResponse>("/api/users/profile");
        Assert.Equal(1, seenByAlice.FollowersCount);
        Assert.Equal(0, seenByAlice.FollowingCount);
        Assert.False(seenByAlice.IsFollowed);

        Assert.Equal(1, (await bob.GetAsync<UserResponse>("/api/users/profile")).FollowingCount);
    }

    // ---- updating the profile -------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateProfile_ChangesFields_AndTheBioCanBeCleared()
    {
        var user = await api.RegisterAsync("editor");

        var updated = await (await user.PutAsync("/api/users/profile", new { displayName = "New Name", bio = "about me", avatarUrl = "https://example.com/a.png" }))
            .ReadAsync<UserResponse>();
        Assert.Equal("New Name", updated.DisplayName);
        Assert.Equal("about me", updated.Bio);
        Assert.Equal("https://example.com/a.png", updated.AvatarUrl);
        Assert.Equal(user.Email, updated.Email);

        // An empty string clears a field (the edit form always sends all three).
        var cleared = await (await user.PutAsync("/api/users/profile", new { displayName = "New Name", bio = "", avatarUrl = "" }))
            .ReadAsync<UserResponse>();
        Assert.Equal("", cleared.Bio);
        Assert.Equal("", cleared.AvatarUrl);
        Assert.Equal("New Name", (await user.GetAsync<UserResponse>("/api/users/profile")).DisplayName);
    }

    [Fact]
    public async Task UpdateProfile_IgnoresABlankDisplayName_AndAcceptsPartialUpdates()
    {
        var user = await api.RegisterAsync("editor");
        var before = await user.GetAsync<UserResponse>("/api/users/profile");

        var response = await user.PutAsync("/api/users/profile", new { displayName = "   ", bio = "only the bio" });
        await response.ShouldBeAsync(HttpStatusCode.OK);

        var after = await response.ReadAsync<UserResponse>();
        Assert.Equal(before.DisplayName, after.DisplayName);
        Assert.Equal("only the bio", after.Bio);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/a.png")]
    [InlineData("/relative/path.png")]
    public async Task UpdateProfile_RejectsAvatarsThatAreNotHttpUrls(string avatarUrl)
    {
        var user = await api.RegisterAsync("editor");

        var response = await user.PutAsync("/api/users/profile", new { displayName = "x", bio = "", avatarUrl });

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.Equal("", (await user.GetAsync<UserResponse>("/api/users/profile")).AvatarUrl);
    }

    [Fact]
    public async Task UpdateProfile_EnforcesLengthLimits_AndRequiresLogin()
    {
        var user = await api.RegisterAsync("editor");

        await (await user.PutAsync("/api/users/profile", new { displayName = new string('x', 101), bio = "" })).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await user.PutAsync("/api/users/profile", new { displayName = "ok", bio = new string('x', 501) })).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await api.Anonymous.PutAsJsonAsync("/api/users/profile", new { displayName = "x" })).ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    // ---- search ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Search_FindsUsersAnonymously_WithoutExposingEmails()
    {
        var target = await api.RegisterAsync("findable");

        var results = await api.Anonymous.GetFromJsonAsync<List<UserResponse>>($"/api/users/search?query={target.Username}", TestUser.Json);

        var found = Assert.Single(results!);
        Assert.Equal(target.Id, found.Id);
        Assert.True(string.IsNullOrEmpty(found.Email));
    }

    [Fact]
    public async Task Search_NeedsAQuery()
    {
        await (await api.Anonymous.GetAsync("/api/users/search")).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await api.Anonymous.GetAsync("/api/users/search?query=%20%20")).ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_TreatsSqlWildcardsAndQuotesAsPlainText()
    {
        // A user whose name has no '%' or '_' in it, so those characters can only match if they act as wildcards.
        var token = Guid.NewGuid().ToString("N")[..10];
        var register = await api.Anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"wild{token}",
            email = $"wild{token}@example.test",
            password = "Passw0rd!x",
        });
        await register.ShouldBeAsync(HttpStatusCode.OK);

        async Task<List<UserResponse>> Search(string query) =>
            await api.Anonymous.GetFromJsonAsync<List<UserResponse>>($"/api/users/search?query={Uri.EscapeDataString(query)}", TestUser.Json) ?? [];

        Assert.Single(await Search($"wild{token}"));                    // control: a plain search finds them
        Assert.Empty(await Search($"wild%{token}"));                    // '%' is not "anything"
        Assert.Empty(await Search($"w_ld{token}"));                     // '_' is not "any one character"
        Assert.Empty(await Search("'; DROP TABLE users; --"));          // injection attempt is just text
        Assert.Single(await Search($"wild{token}"));                    // and the table is still there
    }

    [Fact]
    public async Task Search_PageSizeIsBounded()
    {
        await api.RegisterAsync("bounded");
        await api.RegisterAsync("bounded");

        var minimum = await api.Anonymous.GetFromJsonAsync<List<UserResponse>>("/api/users/search?query=bounded&take=0", TestUser.Json);
        Assert.Single(minimum!);

        var huge = await api.Anonymous.GetFromJsonAsync<List<UserResponse>>("/api/users/search?query=_&take=100000", TestUser.Json);
        Assert.True(huge!.Count <= 50);
    }

    // ---- suggestions ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Suggestions_ExcludeYouAndPeopleYouAlreadyFollow()
    {
        var me = await api.RegisterAsync("me");
        var followed = await api.RegisterAsync("followed");
        var stranger = await api.RegisterAsync("stranger");
        await me.FollowAsync(followed);

        // Suggestions are the newest users first, so on a busy database ask for the maximum and look for the people we care about.
        var suggestions = await me.GetAsync<List<UserResponse>>("/api/users/suggestions?take=20");
        var ids = suggestions.Select(u => u.Id).ToList();

        Assert.DoesNotContain(me.Id, ids);
        Assert.DoesNotContain(followed.Id, ids);
        Assert.Contains(stranger.Id, ids);
        Assert.All(suggestions, u => Assert.True(string.IsNullOrEmpty(u.Email)));
    }

    [Fact]
    public async Task Suggestions_RequireLogin_AndTheirPageSizeIsBounded()
    {
        var me = await api.RegisterAsync("me");
        await api.RegisterAsync("someone");

        await (await api.Anonymous.GetAsync("/api/users/suggestions")).ShouldBeAsync(HttpStatusCode.Unauthorized);

        Assert.Single(await me.GetAsync<List<UserResponse>>("/api/users/suggestions?take=0"));
        Assert.True((await me.GetAsync<List<UserResponse>>("/api/users/suggestions?take=100000")).Count <= 20);
    }
}
