namespace XCloneAPI.Tests.Infrastructure;

/// <summary>Locates the Angular app's source, so tests can check the API against what the frontend actually uses.</summary>
public static class Frontend
{
    public static string? SourceRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "x-clone-frontend", "src", "app");
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(SourceRoot()!, relativePath));
}

/// <summary>A [Fact] that is skipped (not failed) when the frontend folder isn't next to the API, e.g. in a backend-only checkout.</summary>
public sealed class FactWithFrontendAttribute : FactAttribute
{
    public FactWithFrontendAttribute()
    {
        if (Frontend.SourceRoot() == null)
            Skip = "The x-clone-frontend folder was not found next to the API project.";
    }
}
