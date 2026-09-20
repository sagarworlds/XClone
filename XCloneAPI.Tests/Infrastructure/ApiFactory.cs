using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace XCloneAPI.Tests.Infrastructure;

/// <summary>
/// Runs the real API in-process. Configuration comes only from the settings passed in, in the "Testing"
/// environment, so results never depend on your user-secrets or environment variables.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _settings;

    /// <summary>The signing key this host validates tokens with (lets tests mint their own tokens).</summary>
    public string JwtKey => _settings["Jwt:Key"]!;

    public ApiFactory(Dictionary<string, string?> settings)
    {
        _settings = settings;
    }

    /// <summary>Normal settings: the given database, a random signing key, and a rate limit high enough for a test suite.</summary>
    public static ApiFactory ForDatabase(string connectionString, Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Jwt:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
            ["RateLimiting:AuthPermitLimit"] = "100000",
            ["Logging:LogLevel:Default"] = "Warning",
        };

        if (overrides != null)
            foreach (var pair in overrides)
                settings[pair.Key] = pair.Value;

        return new ApiFactory(settings);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        foreach (var pair in _settings)
            builder.UseSetting(pair.Key, pair.Value);
    }
}
