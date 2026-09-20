using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using XCloneAPI.DTOs;
using XCloneAPI.Models;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

[Collection(ApiCollection.Name)]
public class PasswordHashingTests(ApiFixture api)
{
    private Task<string> StoredHashAsync(string username) =>
        api.WithDbAsync(db => db.Users.AsNoTracking().Where(u => u.Username == username).Select(u => u.PasswordHash).SingleAsync());

    private static string LegacySha256(string password) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    /// <summary>Inserts an account the way the very first version of the app stored it: unsalted SHA-256, base64.</summary>
    private async Task<(string Username, string Email)> InsertLegacyUserAsync(string password)
    {
        var name = $"legacy_{Guid.NewGuid():N}"[..20];
        await api.WithDbAsync(async db =>
        {
            db.Users.Add(new User { Username = name, Email = $"{name}@example.test", PasswordHash = LegacySha256(password) });
            await db.SaveChangesAsync();
        });
        return (name, $"{name}@example.test");
    }

    private Task<HttpResponseMessage> LoginAsync(string usernameOrEmail, string password) =>
        api.Anonymous.PostAsJsonAsync("/api/auth/login", new { usernameOrEmail, password });

    [Fact]
    public async Task NewPasswords_AreStoredWithASaltedSlowHash_NeverInPlainText()
    {
        var user = await api.RegisterAsync("hashed", "Sup3r-Secret-Pw");

        var stored = await StoredHashAsync(user.Username);

        Assert.DoesNotContain("Sup3r-Secret-Pw", stored);
        Assert.NotEqual(LegacySha256("Sup3r-Secret-Pw"), stored);
        Assert.StartsWith("AQAAAA", stored);            // ASP.NET Identity v3 format: PBKDF2 with a per-user salt
        Assert.True(stored.Length > 60, $"expected a PBKDF2 hash but got {stored.Length} characters");
    }

    [Fact]
    public async Task TheSamePassword_GivesDifferentHashesForDifferentUsers()
    {
        var a = await api.RegisterAsync("twin", "Identical-Pw-1");
        var b = await api.RegisterAsync("twin", "Identical-Pw-1");

        Assert.NotEqual(await StoredHashAsync(a.Username), await StoredHashAsync(b.Username));
    }

    [Fact]
    public async Task ALegacySha256Account_CanStillLogIn_AndIsUpgradedToTheNewFormat()
    {
        var (username, _) = await InsertLegacyUserAsync("legacy-pass-1");
        var before = await StoredHashAsync(username);
        Assert.Equal(44, before.Length);   // sanity: it really is a legacy hash

        await (await LoginAsync(username, "legacy-pass-1")).ShouldBeAsync(HttpStatusCode.OK);

        var after = await StoredHashAsync(username);
        Assert.StartsWith("AQAAAA", after);
        Assert.NotEqual(before, after);
        await (await LoginAsync(username, "legacy-pass-1")).ShouldBeAsync(HttpStatusCode.OK);   // and the upgraded hash works
        await (await LoginAsync(username, "wrong-pass-1")).ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AFailedLoginOnALegacyAccount_LeavesTheOldHashUntouched()
    {
        var (username, email) = await InsertLegacyUserAsync("legacy-pass-2");
        var before = await StoredHashAsync(username);

        await (await LoginAsync(email, "not-the-password")).ShouldBeAsync(HttpStatusCode.Unauthorized);

        Assert.Equal(before, await StoredHashAsync(username));
    }

    [Fact]
    public async Task APasswordThatOnlyDiffersInCase_IsRejected()
    {
        var user = await api.RegisterAsync("casey", "CaseSensitive-1");

        await (await LoginAsync(user.Username, "casesensitive-1")).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await LoginAsync(user.Username, "CaseSensitive-1")).ShouldBeAsync(HttpStatusCode.OK);
    }
}
