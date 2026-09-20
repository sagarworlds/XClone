using XCloneAPI.Services;

namespace XCloneAPI.Tests;

/// <summary>The folder that holds uploaded images: it keeps to its folder, and never overwrites.</summary>
public sealed class LocalMediaStorageTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"xclone_storage_{Guid.NewGuid():N}");
    private readonly string _outside;

    public LocalMediaStorageTests()
    {
        _outside = Path.Combine(Path.GetTempPath(), $"xclone_outside_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outside);
    }

    public void Dispose()
    {
        foreach (var directory in new[] { _folder, _outside })
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static string NewName() => MediaNames.NewName(ImageKind.Png);

    private static MemoryStream Bytes(params byte[] bytes) => new(bytes);

    [Fact]
    public void ItCreatesItsFolder_EvenSeveralLevelsDeep()
    {
        var deep = Path.Combine(_folder, "a", "b");

        var storage = new LocalMediaStorage(deep);

        Assert.True(Directory.Exists(deep));
        Assert.Equal(Path.GetFullPath(deep), storage.Directory);
    }

    [Fact]
    public async Task AFileIsSavedUnderItsName_ExistsThen_AndIsGoneAfterDelete()
    {
        var storage = new LocalMediaStorage(_folder);
        var name = NewName();
        Assert.False(storage.Exists(name));

        await storage.SaveAsync(name, Bytes(1, 2, 3), CancellationToken.None);

        Assert.True(storage.Exists(name));
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(Path.Combine(_folder, name)));

        storage.Delete(name);
        Assert.False(storage.Exists(name));
        Assert.False(File.Exists(Path.Combine(_folder, name)));
    }

    [Fact]
    public void DeletingWhatIsAlreadyGone_IsNotAnError()
    {
        var storage = new LocalMediaStorage(_folder);

        storage.Delete(NewName());
    }

    [Fact]
    public async Task ANameIsNeverReused_SoNothingCanBeOverwritten()
    {
        var storage = new LocalMediaStorage(_folder);
        var name = NewName();
        await storage.SaveAsync(name, Bytes(1, 1, 1), CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync(name, Bytes(9, 9, 9), CancellationToken.None));

        Assert.Equal(new byte[] { 1, 1, 1 }, await File.ReadAllBytesAsync(Path.Combine(_folder, name)));
    }

    public static IEnumerable<object[]> NamesThatAreNotOurs() => new[]
    {
        "../evil.png",
        "..\\evil.png",
        "sub/0123456789abcdef0123456789abcdef.png",
        "/etc/passwd",
        "C:\\Windows\\win.ini",
        "notes.txt",
        "0123456789abcdef0123456789abcdef.svg",
        "0123456789ABCDEF0123456789abcdef.png",
        "",
    }.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(NamesThatAreNotOurs))]
    public async Task ANameThatWasNotMadeForAnImage_IsRefusedEverywhere_AndTouchesNothing(string name)
    {
        var storage = new LocalMediaStorage(_folder);
        var victim = Path.Combine(_outside, "victim.png");
        await File.WriteAllBytesAsync(victim, new byte[] { 7 });

        Assert.Throws<ArgumentException>(() => storage.Exists(name));
        Assert.Throws<ArgumentException>(() => storage.Delete(name));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(name, Bytes(1), CancellationToken.None));

        Assert.True(File.Exists(victim));
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task AnImageCannotBeReachedThroughAnotherFolder_EvenWithARealNameInFront()
    {
        var storage = new LocalMediaStorage(_folder);
        var name = NewName();
        await File.WriteAllBytesAsync(Path.Combine(_outside, name), new byte[] { 7 });

        Assert.False(storage.Exists(name));
        Assert.Throws<ArgumentException>(() => storage.Exists("../" + Path.GetFileName(_outside) + "/" + name));
    }
}
