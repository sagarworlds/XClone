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

    /// <summary>The folder this host keeps uploaded images in (private to this host, removed when it is disposed).</summary>
    public string UploadsDirectory => _settings["Uploads:Directory"]!;

    public ApiFactory(Dictionary<string, string?> settings)
    {
        _settings = new Dictionary<string, string?>(settings);
        if (!_settings.ContainsKey("Uploads:Directory"))
            _settings["Uploads:Directory"] = Path.Combine(Path.GetTempPath(), $"xclone_it_uploads_{Guid.NewGuid():N}");
    }

    private void RemoveUploads()
    {
        try
        {
            if (Directory.Exists(UploadsDirectory))
                Directory.Delete(UploadsDirectory, recursive: true);
        }
        catch (IOException)
        {
            // a leftover temp folder is not worth failing a test run for
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) RemoveUploads();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        RemoveUploads();
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
