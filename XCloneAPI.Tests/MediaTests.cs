using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using XCloneAPI.DTOs;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>
/// Images on posts: uploading one (what is accepted, what is refused, what the server decides itself), serving it back,
/// attaching it to posts and replies, and cleaning up when the post goes.
/// </summary>
[Collection(ApiCollection.Name)]
public partial class MediaTests(ApiFixture api)
{
    [GeneratedRegex(@"^/uploads/[0-9a-f]{32}\.(png|jpg|gif|webp)\z")]
    private static partial Regex UploadUrl();

    private string UploadsDirectory => api.Factory.UploadsDirectory;

    private static string NameOf(string url) => url["/uploads/".Length..];

    private string PathOf(string url) => Path.Combine(UploadsDirectory, NameOf(url));

    private static async Task<HttpResponseMessage> UploadRawAsync(HttpClient client, HttpContent content) =>
        await client.PostAsync("/api/media", content);

    private static byte[] PngOfSize(int bytes)
    {
        var data = new byte[bytes];
        Array.Copy(SampleImages.Png, data, Math.Min(SampleImages.Png.Length, bytes));
        return data;
    }

    // ---- uploading ---------------------------------------------------------------------------------------------

    public static IEnumerable<object[]> ImageKinds() => new[]
    {
        new object[] { "png", "image/png" },
        new object[] { "jpg", "image/jpeg" },
        new object[] { "gif", "image/gif" },
        new object[] { "webp", "image/webp" },
    };

    private static byte[] SampleFor(string extension) => extension switch
    {
        "png" => SampleImages.Png,
        "jpg" => SampleImages.Jpeg,
        "gif" => SampleImages.Gif,
        _ => SampleImages.Webp,
    };

    [Theory]
    [MemberData(nameof(ImageKinds))]
    public async Task AnImage_IsStored_AndServedBackExactlyAsUploaded_ToAnyone(string extension, string contentType)
    {
        var alice = await api.RegisterAsync("alice");
        var bytes = SampleFor(extension);

        var url = await alice.UploadImageAsync(bytes, $"holiday.{extension}", contentType);

        Assert.Matches(UploadUrl(), url);
        Assert.EndsWith("." + extension, url);
        var served = await api.Anonymous.GetAsync(url);
        await served.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal(contentType, served.Content.Headers.ContentType?.MediaType);
        Assert.Equal(bytes, await served.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task AServedImage_CannotBeReinterpretedByTheBrowser_AndMayBeCachedForGood()
    {
        var alice = await api.RegisterAsync("alice");
        var url = await alice.UploadImageAsync();

        var served = await api.Anonymous.GetAsync(url);

        Assert.Equal("nosniff", served.Headers.GetValues("X-Content-Type-Options").Single());
        var cache = served.Headers.CacheControl!;
        Assert.True(cache.Public);
        Assert.True(cache.MaxAge >= TimeSpan.FromDays(365));
        Assert.Contains("immutable", cache.ToString());
    }

    [Fact]
    public async Task WhatTheFileIs_NotWhatTheClientCallsIt_DecidesItsKind()
    {
        var alice = await api.RegisterAsync("alice");

        // PNG bytes that claim to be a JPEG
        var url = await alice.UploadImageAsync(SampleImages.Png, "photo.jpg", "image/jpeg");

        Assert.EndsWith(".png", url);
        var served = await api.Anonymous.GetAsync(url);
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AnImageWithNoName_OrNoContentType_IsStillAnImage()
    {
        var alice = await api.RegisterAsync("alice");
        var file = new ByteArrayContent(SampleImages.Gif);   // no content type header at all
        var form = new MultipartFormDataContent { { file, "file", "x" } };

        var response = await UploadRawAsync(alice.Client, form);

        await response.ShouldBeAsync(HttpStatusCode.OK);
        Assert.EndsWith(".gif", (await response.ReadAsync<MediaResponseDto>()).Url);
    }

    public static IEnumerable<object[]> NotImages() => new[]
    {
        new object[] { "a text file called .png", Encoding.ASCII.GetBytes("just some text"), "note.png", "image/png" },
        new object[] { "html called .png", Encoding.ASCII.GetBytes("<html><script>alert(1)</script></html>"), "page.png", "image/png" },
        new object[] { "an svg, even as image/svg+xml", Encoding.ASCII.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"/>"), "logo.svg", "image/svg+xml" },
        new object[] { "an svg pretending to be a png", Encoding.ASCII.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"/>"), "logo.png", "image/png" },
        new object[] { "a PNG cut short", SampleImages.Png[..7], "cut.png", "image/png" },
        new object[] { "a GIF cut short", SampleImages.Gif[..4], "cut.gif", "image/gif" },
        new object[] { "a PDF", Encoding.ASCII.GetBytes("%PDF-1.7 test test"), "doc.pdf", "application/pdf" },
    };

    [Theory]
    [MemberData(nameof(NotImages))]
    public async Task ANonImage_IsRefused_WhateverItIsCalled_AndNothingIsStored(string _, byte[] bytes, string fileName, string contentType)
    {
        var alice = await api.RegisterAsync("alice");
        var before = Directory.GetFiles(UploadsDirectory).Length;

        var response = await alice.Client.UploadAsync(bytes, fileName, contentType);

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.Equal("Only PNG, JPEG, GIF and WebP images can be uploaded.", await response.ReadMessageAsync());
        Assert.Equal(before, Directory.GetFiles(UploadsDirectory).Length);
    }

    [Fact]
    public async Task AnEmptyFile_IsRefused()
    {
        var alice = await api.RegisterAsync("alice");

        var response = await alice.Client.UploadAsync(Array.Empty<byte>());

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.Equal("Choose an image to upload.", await response.ReadMessageAsync());
    }

    [Fact]
    public async Task ARequestWithoutTheFileField_IsRefused()
    {
        var alice = await api.RegisterAsync("alice");

        var wrongField = await alice.Client.UploadAsync(SampleImages.Png, field: "picture");
        var noParts = await UploadRawAsync(alice.Client, new MultipartFormDataContent());

        await wrongField.ShouldBeAsync(HttpStatusCode.BadRequest);
        await noParts.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ARequestThatIsNotAFileUpload_IsRefused()
    {
        var alice = await api.RegisterAsync("alice");
        var before = Directory.GetFiles(UploadsDirectory).Length;

        var json = await alice.PostAsync("/api/media", new { url = "https://example.com/x.png" });

        await json.ShouldBeAsync(HttpStatusCode.BadRequest);   // a clear answer, not a misleading "Endpoint not found"
        Assert.Equal("Choose an image to upload.", await json.ReadMessageAsync());
        Assert.Equal(before, Directory.GetFiles(UploadsDirectory).Length);
    }

    [Fact]
    public async Task ExactlyTheLimit_IsAccepted_AndOneByteMoreIsNot()
    {
        var alice = await api.RegisterAsync("alice");
        const int limit = 5 * 1024 * 1024;

        var atLimit = await alice.Client.UploadAsync(PngOfSize(limit));
        var over = await alice.Client.UploadAsync(PngOfSize(limit + 1));

        await atLimit.ShouldBeAsync(HttpStatusCode.OK);
        await over.ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.Equal("Images can be at most 5 MB.", await over.ReadMessageAsync());
    }

    [Fact]
    public async Task AFarTooBigUpload_IsRefused_NotSwallowed()
    {
        var alice = await api.RegisterAsync("alice");

        var response = await alice.Client.UploadAsync(PngOfSize(8 * 1024 * 1024));

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge, $"got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task UploadingRequiresLogin()
    {
        var response = await api.Anonymous.UploadAsync(SampleImages.Png);

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EveryUpload_GetsItsOwnName_EvenTheSameFileTwice()
    {
        var alice = await api.RegisterAsync("alice");

        var first = await alice.UploadImageAsync();
        var second = await alice.UploadImageAsync();

        Assert.NotEqual(first, second);
        Assert.True(File.Exists(PathOf(first)) && File.Exists(PathOf(second)));
    }

    [Theory]
    [InlineData("../../evil.png")]
    [InlineData("..\\..\\evil.png")]
    [InlineData("/etc/passwd.png")]
    [InlineData("C:\\Windows\\win.png")]
    [InlineData("a b c.png")]
    [InlineData("evil.png\0.txt")]
    [InlineData("%2e%2e%2fevil.png")]
    public async Task TheClientsFileName_NeverReachesTheDisk(string fileName)
    {
        var alice = await api.RegisterAsync("alice");

        var response = await alice.Client.UploadAsync(SampleImages.Png, fileName.Replace("\0", ""));

        await response.ShouldBeAsync(HttpStatusCode.OK);
        var url = (await response.ReadAsync<MediaResponseDto>()).Url;
        Assert.Matches(UploadUrl(), url);
        Assert.True(File.Exists(PathOf(url)));
        Assert.All(Directory.GetFiles(UploadsDirectory), f => Assert.Matches(@"^[0-9a-f]{32}\.(png|jpg|gif|webp)$", Path.GetFileName(f)));
        Assert.False(File.Exists(Path.Combine(UploadsDirectory, "..", "evil.png")));
        Assert.False(File.Exists(Path.Combine(UploadsDirectory, "evil.png")));
    }

    // ---- what is served from /uploads ------------------------------------------------------------------------

    [Fact]
    public async Task OnlyImagesAreServed_NothingElseThatHappensToBeInTheFolder()
    {
        var stray = Path.Combine(UploadsDirectory, "notes.txt");
        await File.WriteAllTextAsync(stray, "secret notes");
        var strayImage = Path.Combine(UploadsDirectory, "logo.svg");
        await File.WriteAllTextAsync(strayImage, "<svg/>");

        await (await api.Anonymous.GetAsync("/uploads/notes.txt")).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await api.Anonymous.GetAsync("/uploads/logo.svg")).ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/uploads/")]
    [InlineData("/uploads")]
    [InlineData("/uploads/00000000000000000000000000000000.png")]
    [InlineData("/uploads/../appsettings.json")]
    [InlineData("/uploads/%2e%2e/appsettings.json")]
    [InlineData("/uploads/..%2fappsettings.json")]
    [InlineData("/uploads/%2e%2e%2f%2e%2e%2fProgram.cs")]
    public async Task NoListing_NoMissingFile_NoWayOutOfTheFolder(string path)
    {
        var response = await api.Anonymous.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnImageRequestedFromTheAngularAppsOrigin_IsServed()
    {
        var alice = await api.RegisterAsync("alice");
        var url = await alice.UploadImageAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Origin", "http://localhost:4200");

        var response = await api.Anonymous.SendAsync(request);

        await response.ShouldBeAsync(HttpStatusCode.OK);   // an <img> on the Angular app's page is a plain GET from that origin
    }

    // ---- attaching images to posts ---------------------------------------------------------------------------

    [Fact]
    public async Task APost_CanCarryUploadedImages_InTheOrderGiven()
    {
        var alice = await api.RegisterAsync("alice");
        var first = await alice.UploadImageAsync(SampleImages.Png);
        var second = await alice.UploadImageAsync(SampleImages.Gif, "b.gif", "image/gif");

        var post = await alice.CreatePostWithImagesAsync("two pictures", second, first);

        Assert.Equal(new[] { second, first }, post.MediaUrls);
        Assert.Equal(new[] { second, first }, (await api.Anonymous.GetFromJsonAsync<PostResponse>($"/api/posts/{post.Id}", TestUser.Json))!.MediaUrls);
        Assert.Equal(new[] { second, first }, (await alice.FeedAsync()).Single(p => p.Id == post.Id).MediaUrls);
    }

    [Fact]
    public async Task AReply_CanCarryImagesToo()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var post = await alice.CreatePostAsync("root");
        var image = await bob.UploadImageAsync();

        var reply = await bob.ReplyWithImagesAsync(post.Id, "look", image);

        Assert.Equal(new[] { image }, reply.MediaUrls);
        var thread = await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/{post.Id}/replies");
        Assert.Equal(new[] { image }, thread.Single().MediaUrls);
    }

    [Fact]
    public async Task FourImagesIsTheMostAPostCanHave()
    {
        var alice = await api.RegisterAsync("alice");
        var four = new List<string>();
        for (var i = 0; i < 4; i++) four.Add(await alice.UploadImageAsync());

        var post = await alice.CreatePostWithImagesAsync("four", four.ToArray());

        Assert.Equal(4, post.MediaUrls.Length);
    }

    [Fact]
    public async Task ImagesAreOptional()
    {
        var alice = await api.RegisterAsync("alice");

        var nothing = await alice.PostAsync("/api/posts", new { content = "no images" });
        var empty = await alice.PostAsync("/api/posts", new { content = "empty list", mediaUrls = Array.Empty<string>() });
        var nullList = await alice.PostAsync("/api/posts", new { content = "null list", mediaUrls = (string[]?)null });

        await nothing.ShouldBeAsync(HttpStatusCode.Created);
        await empty.ShouldBeAsync(HttpStatusCode.Created);
        await nullList.ShouldBeAsync(HttpStatusCode.Created);
        Assert.Empty((await nothing.ReadAsync<PostResponse>()).MediaUrls);
    }

    [Fact]
    public async Task AnotherUsersUpload_CanBeAttached_BecauseTheAddressIsPublicAnyway()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var image = await alice.UploadImageAsync();

        var post = await bob.CreatePostWithImagesAsync("reusing a picture", image);

        Assert.Equal(new[] { image }, post.MediaUrls);
    }

    public static IEnumerable<object[]> AddressesThatAreWrongOnTheirOwn() => new[]
    {
        "https://example.com/pic.png",
        "http://example.com/pic.png",
        "javascript:alert(1)",
        "data:image/png;base64,AAAA",
        "/uploads/00000000000000000000000000000000.png",   // looks right, but nothing was uploaded under that name
        "/uploads/../appsettings.json",
        "/uploads/00000000000000000000000000000000.svg",
        "nope",
        "",
    }.Select(url => new object[] { url });

    [Theory]
    [MemberData(nameof(AddressesThatAreWrongOnTheirOwn))]
    public async Task APost_RefusesAnythingButAnUploadedImage(string address)
    {
        var alice = await api.RegisterAsync("alice");
        var root = await alice.CreatePostAsync("root");

        var post = await alice.PostAsync("/api/posts", new { content = "should not be created", mediaUrls = new[] { address } });
        var reply = await alice.PostAsync($"/api/posts/{root.Id}/replies", new { content = "should not be created", mediaUrls = new[] { address } });

        await post.ShouldBeAsync(HttpStatusCode.BadRequest);
        await reply.ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.DoesNotContain(await alice.FeedAsync(), p => p.Content == "should not be created");
        Assert.Empty(await api.Anonymous.GetItemsAsync<PostResponse>($"/api/posts/{root.Id}/replies"));
    }

    [Fact]
    public async Task APost_RefusesAGoodImageAlongsideABadOne()
    {
        var alice = await api.RegisterAsync("alice");
        var good = await alice.UploadImageAsync();

        var response = await alice.PostAsync("/api/posts", new { content = "mixed", mediaUrls = new[] { good, "https://example.com/pic.png" } });

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task APost_RefusesFiveImages_AndTheSameImageTwice()
    {
        var alice = await api.RegisterAsync("alice");
        var five = new List<string>();
        for (var i = 0; i < 5; i++) five.Add(await alice.UploadImageAsync());

        var tooMany = await alice.PostAsync("/api/posts", new { content = "five", mediaUrls = five });
        var twice = await alice.PostAsync("/api/posts", new { content = "twice", mediaUrls = new[] { five[0], five[0] } });

        await tooMany.ShouldBeAsync(HttpStatusCode.BadRequest);
        await twice.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task APost_RefusesARealImageWrittenAnyOtherWayThanTheAddressItWasGiven()
    {
        var alice = await api.RegisterAsync("alice");
        var real = await alice.UploadImageAsync();

        var variants = new[]
        {
            real + "?x=1",                                      // a query added
            real + "#top",                                      // a fragment added
            "http://localhost" + real,                          // as a full address
            "/uploads/" + NameOf(real).ToUpperInvariant(),      // in capitals
            real + "/",                                         // a trailing slash
            "/uploads//" + NameOf(real),                        // a doubled slash
            " " + real,                                         // a leading space
        };

        foreach (var variant in variants)
        {
            var response = await alice.PostAsync("/api/posts", new { content = "dressed up", mediaUrls = new[] { variant } });
            await response.ShouldBeAsync(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task AnImageThatWasRemovedFromTheFolder_CannotBeAttached()
    {
        var alice = await api.RegisterAsync("alice");
        var image = await alice.UploadImageAsync();
        File.Delete(PathOf(image));

        var response = await alice.PostAsync("/api/posts", new { content = "gone", mediaUrls = new[] { image } });

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
        Assert.Equal("An attached image was not found. Upload it again.", await response.ReadMessageAsync());
    }

    // ---- cleaning up ------------------------------------------------------------------------------------------

    [Fact]
    public async Task DeletingAPost_RemovesItsImages_AndThoseOfEveryReplyBelowIt_ButNoOthers()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var carol = await api.RegisterAsync("carol");
        var postImage = await alice.UploadImageAsync();
        var replyImage = await bob.UploadImageAsync();
        var deepReplyImage = await carol.UploadImageAsync();
        var otherImage = await alice.UploadImageAsync();
        var post = await alice.CreatePostWithImagesAsync("with a picture", postImage);
        var reply = await bob.ReplyWithImagesAsync(post.Id, "reply with a picture", replyImage);
        await carol.ReplyWithImagesAsync(reply.Id, "a reply to the reply", deepReplyImage);
        await alice.CreatePostWithImagesAsync("another post", otherImage);

        await (await alice.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        foreach (var gone in new[] { postImage, replyImage, deepReplyImage })
        {
            Assert.False(File.Exists(PathOf(gone)), gone);
            await (await api.Anonymous.GetAsync(gone)).ShouldBeAsync(HttpStatusCode.NotFound);
        }

        await (await api.Anonymous.GetAsync(otherImage)).ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeletingAReply_RemovesOnlyItsOwnImages()
    {
        var alice = await api.RegisterAsync("alice");
        var bob = await api.RegisterAsync("bob");
        var postImage = await alice.UploadImageAsync();
        var replyImage = await bob.UploadImageAsync();
        var post = await alice.CreatePostWithImagesAsync("root", postImage);
        var reply = await bob.ReplyWithImagesAsync(post.Id, "reply", replyImage);

        await (await bob.DeleteAsync($"/api/posts/{reply.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        Assert.False(File.Exists(PathOf(replyImage)));
        Assert.True(File.Exists(PathOf(postImage)));
    }

    [Fact]
    public async Task ADeleteThatIsRefused_KeepsTheImages()
    {
        var alice = await api.RegisterAsync("alice");
        var mallory = await api.RegisterAsync("mallory");
        var image = await alice.UploadImageAsync();
        var post = await alice.CreatePostWithImagesAsync("mine", image);

        await (await mallory.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NotFound);

        Assert.True(File.Exists(PathOf(image)));
        Assert.Equal(new[] { image }, (await alice.GetPostAsync(post.Id)).MediaUrls);
    }

    [Fact]
    public async Task DeletingAPostWhoseImageIsAlreadyGone_StillWorks()
    {
        var alice = await api.RegisterAsync("alice");
        var image = await alice.UploadImageAsync();
        var post = await alice.CreatePostWithImagesAsync("mine", image);
        File.Delete(PathOf(image));

        await (await alice.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AnUploadNoPostUsesYet_IsLeftAlone()
    {
        var alice = await api.RegisterAsync("alice");
        var image = await alice.UploadImageAsync();
        var post = await alice.CreatePostAsync("unrelated");

        await (await alice.DeleteAsync($"/api/posts/{post.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);

        Assert.True(File.Exists(PathOf(image)));
    }

    // ---- the limit on uploads ---------------------------------------------------------------------------------

    [Fact]
    public async Task UploadsAreLimitedPerUser_NotForEveryone()
    {
        await using var factory = ApiFactory.ForDatabase(api.ConnectionString, new() { ["RateLimiting:UploadPermitLimit"] = "2" });
        var alice = await TestUser.RegisterAsync(factory, "alice", "Passw0rd!x");
        var bob = await TestUser.RegisterAsync(factory, "bob", "Passw0rd!x");

        await (await alice.Client.UploadAsync(SampleImages.Png)).ShouldBeAsync(HttpStatusCode.OK);
        await (await alice.Client.UploadAsync(SampleImages.Png)).ShouldBeAsync(HttpStatusCode.OK);
        var blocked = await alice.Client.UploadAsync(SampleImages.Png);

        await blocked.ShouldBeAsync(HttpStatusCode.TooManyRequests);
        Assert.Contains("Too many attempts", await blocked.ReadMessageAsync());
        await (await bob.Client.UploadAsync(SampleImages.Png)).ShouldBeAsync(HttpStatusCode.OK);   // bob has his own allowance
    }

    [Fact]
    public async Task TheLimitDoesNotTouchAnythingButUploads()
    {
        await using var factory = ApiFactory.ForDatabase(api.ConnectionString, new() { ["RateLimiting:UploadPermitLimit"] = "1" });
        var alice = await TestUser.RegisterAsync(factory, "alice", "Passw0rd!x");

        await (await alice.Client.UploadAsync(SampleImages.Png)).ShouldBeAsync(HttpStatusCode.OK);
        await (await alice.Client.UploadAsync(SampleImages.Png)).ShouldBeAsync(HttpStatusCode.TooManyRequests);

        for (var i = 0; i < 5; i++)
            await alice.CreatePostAsync($"still fine {i}");
    }

    [Fact]
    public async Task RefusedUploads_CountTowardsTheLimitToo()
    {
        await using var factory = ApiFactory.ForDatabase(api.ConnectionString, new() { ["RateLimiting:UploadPermitLimit"] = "2" });
        var alice = await TestUser.RegisterAsync(factory, "alice", "Passw0rd!x");

        await (await alice.Client.UploadAsync(Encoding.ASCII.GetBytes("not an image"))).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await alice.Client.UploadAsync(Encoding.ASCII.GetBytes("not an image"))).ShouldBeAsync(HttpStatusCode.BadRequest);

        await (await alice.Client.UploadAsync(SampleImages.Png)).ShouldBeAsync(HttpStatusCode.TooManyRequests);
    }
}
