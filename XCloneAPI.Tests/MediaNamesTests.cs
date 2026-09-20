using System.Text;
using XCloneAPI.Services;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>The rules that decide what counts as an image and what an uploaded file may be called (no server needed).</summary>
public class MediaNamesTests
{
    private static byte[] Bytes(params int[] values) => values.Select(v => (byte)v).ToArray();

    private static string Name(char c = 'a', string extension = "png") => new string(c, 32) + "." + extension;

    // ---- what counts as an image ------------------------------------------------------------------------------

    [Fact]
    public void TheFourImageKinds_AreRecognisedByTheirSignature()
    {
        Assert.Equal(ImageKind.Png, MediaNames.Detect(SampleImages.Png));
        Assert.Equal(ImageKind.Jpeg, MediaNames.Detect(SampleImages.Jpeg));
        Assert.Equal(ImageKind.Gif, MediaNames.Detect(SampleImages.Gif));
        Assert.Equal(ImageKind.Webp, MediaNames.Detect(SampleImages.Webp));
    }

    [Fact]
    public void BothGifVersionsCount()
    {
        Assert.Equal(ImageKind.Gif, MediaNames.Detect(Encoding.ASCII.GetBytes("GIF87a....")));
        Assert.Equal(ImageKind.Gif, MediaNames.Detect(Encoding.ASCII.GetBytes("GIF89a....")));
    }

    public static IEnumerable<object[]> NotImages() => new[]
    {
        new object[] { "nothing at all", Array.Empty<byte>() },
        new object[] { "a text file", Encoding.ASCII.GetBytes("hello world, this is text") },
        new object[] { "html", Encoding.ASCII.GetBytes("<!DOCTYPE html><html><script>alert(1)</script>") },
        new object[] { "svg", Encoding.ASCII.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>") },
        new object[] { "an svg with an xml prolog", Encoding.ASCII.GetBytes("<?xml version=\"1.0\"?><svg/>") },
        new object[] { "a PDF", Encoding.ASCII.GetBytes("%PDF-1.7 ....") },
        new object[] { "a zip", Bytes(0x50, 0x4B, 0x03, 0x04, 0, 0, 0, 0, 0, 0, 0, 0) },
        new object[] { "an exe", Bytes(0x4D, 0x5A, 0x90, 0, 3, 0, 0, 0, 4, 0, 0, 0) },
        new object[] { "a PNG cut short", Bytes(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A) },
        new object[] { "a PNG with a damaged signature", Bytes(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0B) },
        new object[] { "a JPEG cut short", Bytes(0xFF, 0xD8) },
        new object[] { "a JPEG missing the third byte", Bytes(0xFF, 0xD8, 0x00, 0xE0) },
        new object[] { "a GIF cut short", Encoding.ASCII.GetBytes("GIF8") },
        new object[] { "a GIF of an unknown version", Encoding.ASCII.GetBytes("GIF86a....") },
        new object[] { "a GIF without the trailing a", Encoding.ASCII.GetBytes("GIF89b....") },
        new object[] { "a RIFF that is not WebP (a WAV)", Encoding.ASCII.GetBytes("RIFF....WAVEfmt ") },
        new object[] { "a WebP cut short", Encoding.ASCII.GetBytes("RIFF....WEB") },
        new object[] { "WEBP without RIFF", Encoding.ASCII.GetBytes("XXXX....WEBP") },
    };

    [Theory]
    [MemberData(nameof(NotImages))]
    public void EverythingElse_IsNotAnImage(string _, byte[] bytes)
    {
        Assert.Null(MediaNames.Detect(bytes));
    }

    [Fact]
    public void OnlyTheStartOfTheFileMatters_SoAHeaderBufferIsEnough()
    {
        var header = new byte[MediaNames.HeaderLength];
        Array.Copy(SampleImages.Webp, header, MediaNames.HeaderLength);

        Assert.Equal(ImageKind.Webp, MediaNames.Detect(header));
    }

    // ---- names and addresses ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(ImageKind.Png, "png", "image/png")]
    [InlineData(ImageKind.Jpeg, "jpg", "image/jpeg")]
    [InlineData(ImageKind.Gif, "gif", "image/gif")]
    [InlineData(ImageKind.Webp, "webp", "image/webp")]
    public void EachKindHasItsExtensionAndContentType(ImageKind kind, string extension, string contentType)
    {
        Assert.Equal(extension, MediaNames.Extension(kind));
        Assert.Equal(contentType, MediaNames.ContentType(kind));
    }

    [Fact]
    public void ANewName_IsRandom_AndValid()
    {
        var names = Enumerable.Range(0, 50).Select(_ => MediaNames.NewName(ImageKind.Jpeg)).ToList();

        Assert.Equal(50, names.Distinct().Count());
        Assert.All(names, n => Assert.True(MediaNames.IsValidName(n), n));
        Assert.All(names, n => Assert.EndsWith(".jpg", n));
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef.png", true)]
    [InlineData("0123456789abcdef0123456789abcdef.jpg", true)]
    [InlineData("0123456789abcdef0123456789abcdef.gif", true)]
    [InlineData("0123456789abcdef0123456789abcdef.webp", true)]
    [InlineData("0123456789ABCDEF0123456789abcdef.png", false)]   // capitals
    [InlineData("0123456789abcdef0123456789abcde.png", false)]    // one short
    [InlineData("0123456789abcdef0123456789abcdef0.png", false)]  // one long
    [InlineData("0123456789abcdef0123456789abcdeg.png", false)]   // not hex
    [InlineData("0123456789abcdef0123456789abcdef.svg", false)]
    [InlineData("0123456789abcdef0123456789abcdef.jpeg", false)]
    [InlineData("0123456789abcdef0123456789abcdef.png.exe", false)]
    [InlineData("0123456789abcdef0123456789abcdef", false)]
    [InlineData("0123456789abcdef0123456789abcdef.png\n", false)]  // $ would let a trailing newline through
    [InlineData("../0123456789abcdef0123456789abcdef.png", false)]
    [InlineData("sub/0123456789abcdef0123456789abcdef.png", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyNamesMadeByTheServerAreValid(string? name, bool valid)
    {
        Assert.Equal(valid, MediaNames.IsValidName(name));
    }

    [Fact]
    public void AnAddressIsTheFolderPlusTheName_AndNothingElseIsAccepted()
    {
        var name = Name('1');

        Assert.Equal("/uploads/" + name, MediaNames.ToUrl(name));
        Assert.True(MediaNames.TryGetName("/uploads/" + name, out var back));
        Assert.Equal(name, back);
    }

    [Theory]
    [InlineData("https://example.com/uploads/0123456789abcdef0123456789abcdef.png")]
    [InlineData("http://localhost:5168/uploads/0123456789abcdef0123456789abcdef.png")]
    [InlineData("//example.com/uploads/0123456789abcdef0123456789abcdef.png")]
    [InlineData("uploads/0123456789abcdef0123456789abcdef.png")]
    [InlineData("/Uploads/0123456789abcdef0123456789abcdef.png")]
    [InlineData("/uploads//0123456789abcdef0123456789abcdef.png")]
    [InlineData("/uploads/../uploads/0123456789abcdef0123456789abcdef.png")]
    [InlineData("/uploads/0123456789abcdef0123456789abcdef.png?x=1")]
    [InlineData("/uploads/0123456789abcdef0123456789abcdef.png#x")]
    [InlineData("/uploads/0123456789abcdef0123456789abcdef.png/")]
    [InlineData("/uploads/")]
    [InlineData("/uploads")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherAddresses_AreNotAcceptedAsUploads(string? url)
    {
        Assert.False(MediaNames.TryGetName(url, out var name));
        Assert.Equal("", name);
    }
}
