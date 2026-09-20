using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFixture api)
{
    private static object NewRegistration(string suffix, string? password = "Passw0rd!x") => new
    {
        username = $"reg_{suffix}",
        email = $"reg_{suffix}@example.test",
        password,
    };

    private static string Suffix() => Guid.NewGuid().ToString("N")[..10];

    // ---- register -------------------------------------------------------------------------------------------

    [Fact]
    public async Task Register_ReturnsTheFlatPayloadTheFrontendExpects()
    {
        var response = await api.Anonymous.PostAsJsonAsync("/api/auth/register", NewRegistration(Suffix()));
        await response.ShouldBeAsync(HttpStatusCode.OK);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var keys = json.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        // The Angular AuthResponse type is flat ({ id, username, ..., token }); a nested "user" would break login.
        Assert.Superset(new HashSet<string> { "id", "username", "email", "displayName", "avatarUrl", "token", "expiresAt" }, keys);
        Assert.DoesNotContain("user", keys);
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task Register_WithoutDisplayName_UsesTheUsername()
    {
        var suffix = Suffix();
        var response = await api.Anonymous.PostAsJsonAsync("/api/auth/register", NewRegistration(suffix));
        await response.ShouldBeAsync(HttpStatusCode.OK);

        Assert.Equal($"reg_{suffix}", (await response.ReadAsync<AuthResponse>()).DisplayName);
    }

    [Fact]
    public async Task Register_StoresTheOptionalBio()
    {
        var suffix = Suffix();
        var response = await api.Anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"bio_{suffix}",
            email = $"bio_{suffix}@example.test",
            password = "Passw0rd!x",
            bio = "hello there",
        });
        await response.ShouldBeAsync(HttpStatusCode.OK);
        var auth = await response.ReadAsync<AuthResponse>();

        using var client = api.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var profile = await client.GetFromJsonAsync<UserResponse>("/api/users/profile", TestUser.Json);

        Assert.Equal("hello there", profile!.Bio);
    }

    [Fact]
    public async Task Register_RejectsADuplicateEmailAndUsername()
    {
        var existing = await api.RegisterAsync("dup");

        var sameEmail = await api.Anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"other_{Suffix()}",
            email = existing.Email,
            password = "Passw0rd!x",
        });
        await sameEmail.ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.Equal("Email already registered", await sameEmail.ReadMessageAsync());

        var sameUsername = await api.Anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            username = existing.Username,
            email = $"other_{Suffix()}@example.test",
            password = "Passw0rd!x",
        });
        await sameUsername.ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.Equal("Username already taken", await sameUsername.ReadMessageAsync());
    }

    [Theory]
    [InlineData("ab", "ok@example.test", "Passw0rd!x")]        // username too short
    [InlineData("validname", "not-an-email", "Passw0rd!x")]    // invalid email
    [InlineData("validname", "ok@example.test", "12345")]      // password too short
    [InlineData("validname", "ok@example.test", null)]         // password missing
    public async Task Register_RejectsInvalidInput(string username, string email, string? password)
    {
        var response = await api.Anonymous.PostAsJsonAsync("/api/auth/register", new { username, email, password });
        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    // ---- login ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Login_WorksWithUsernameOrEmail()
    {
        var user = await api.RegisterAsync("login");

        foreach (var identifier in new[] { user.Username, user.Email })
        {
            var response = await api.Anonymous.PostAsJsonAsync("/api/auth/login", new { usernameOrEmail = identifier, password = user.Password });
            await response.ShouldBeAsync(HttpStatusCode.OK);

            var auth = await response.ReadAsync<AuthResponse>();
            Assert.Equal(user.Id, auth.Id);
            Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        }
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownUser_GetTheSameGenericError()
    {
        var user = await api.RegisterAsync("login");

        var wrongPassword = await api.Anonymous.PostAsJsonAsync("/api/auth/login", new { usernameOrEmail = user.Username, password = "not-the-password" });
        var unknownUser = await api.Anonymous.PostAsJsonAsync("/api/auth/login", new { usernameOrEmail = $"nobody_{Suffix()}", password = "whatever1" });

        await wrongPassword.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await unknownUser.ShouldBeAsync(HttpStatusCode.Unauthorized);

        // Same message either way, so the API doesn't reveal which usernames exist.
        Assert.Equal(await wrongPassword.ReadMessageAsync(), await unknownUser.ReadMessageAsync());
    }

    [Fact]
    public async Task Login_RequiresBothFields()
    {
        var response = await api.Anonymous.PostAsJsonAsync("/api/auth/login", new { usernameOrEmail = "someone" });
        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    // ---- tokens ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ProtectedEndpoint_AcceptsTheIssuedToken_AndRejectsMissingOrGarbageTokens()
    {
        var user = await api.RegisterAsync("token");

        var ok = await user.GetAsync("/api/users/profile");
        await ok.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal(user.Username, (await ok.ReadAsync<UserResponse>()).Username);

        await (await api.Anonymous.GetAsync("/api/users/profile")).ShouldBeAsync(HttpStatusCode.Unauthorized);

        using var garbage = api.Factory.CreateClient();
        garbage.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "abc.def.ghi");
        await (await garbage.GetAsync("/api/users/profile")).ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ATokenMintedWithTheRightKey_IsAccepted_SoTheNegativeCasesBelowAreMeaningful()
    {
        var user = await api.RegisterAsync("mint");
        var token = MintToken(api.Factory.JwtKey, user.Id);

        await (await GetProfileAsync(token)).ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ATokenSignedWithAnotherKey_IsRejected()
    {
        var user = await api.RegisterAsync("forged");
        var forged = MintToken("a-completely-different-signing-key-of-sufficient-length-0123456789", user.Id);

        await (await GetProfileAsync(forged)).ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnExpiredToken_IsRejected()
    {
        var user = await api.RegisterAsync("expired");
        var expired = MintToken(api.Factory.JwtKey, user.Id, notBefore: DateTime.UtcNow.AddHours(-2), expires: DateTime.UtcNow.AddHours(-1));

        await (await GetProfileAsync(expired)).ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("someone-else", "x-clone-app")]   // wrong issuer
    [InlineData("x-clone-api", "someone-else")]   // wrong audience
    public async Task ATokenForAnotherIssuerOrAudience_IsRejected(string issuer, string audience)
    {
        var user = await api.RegisterAsync("aud");
        var token = MintToken(api.Factory.JwtKey, user.Id, issuer: issuer, audience: audience);

        await (await GetProfileAsync(token)).ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> GetProfileAsync(string token)
    {
        using var client = api.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync("/api/users/profile");
    }

    private static string MintToken(
        string key, int userId, string issuer = "x-clone-api", string audience = "x-clone-app",
        DateTime? notBefore = null, DateTime? expires = null)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer, audience,
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            expires ?? DateTime.UtcNow.AddHours(1),
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
