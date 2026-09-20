using System.ComponentModel.DataAnnotations;
using XCloneAPI.DTOs;

namespace XCloneAPI.Tests;

/// <summary>
/// The rule on a post's list of images, tested on its own. (Through the API the same rule is checked a second time,
/// against the files that really exist, so these cases would not show if the first check broke.)
/// </summary>
public class UploadedMediaAttributeTests
{
    private static string Url(char c, string extension = "png") => $"/uploads/{new string(c, 32)}.{extension}";

    private static bool IsValid(object? value)
    {
        var request = new PostRequest { Content = "x" };
        var context = new ValidationContext(request) { MemberName = nameof(PostRequest.MediaUrls) };
        return new UploadedMediaAttribute().GetValidationResult(value, context) == ValidationResult.Success;
    }

    [Fact]
    public void NothingAtAll_IsFine()
    {
        Assert.True(IsValid(null));
        Assert.True(IsValid(Array.Empty<string>()));
    }

    [Fact]
    public void UpToFourUploadedImages_AreFine()
    {
        Assert.True(IsValid(new[] { Url('a') }));
        Assert.True(IsValid(new[] { Url('a'), Url('b', "jpg") }));
        Assert.True(IsValid(new[] { Url('a'), Url('b'), Url('c', "gif"), Url('d', "webp") }));
    }

    [Fact]
    public void FiveImages_AreOneTooMany()
    {
        Assert.False(IsValid(new[] { Url('a'), Url('b'), Url('c'), Url('d'), Url('e') }));
        Assert.Equal(4, UploadedMediaAttribute.MaxItems);
    }

    [Fact]
    public void TheSameImageTwice_IsRefused()
    {
        Assert.False(IsValid(new[] { Url('a'), Url('a') }));
        Assert.False(IsValid(new[] { Url('a'), Url('b'), Url('a') }));
    }

    [Theory]
    [InlineData("https://example.com/pic.png")]
    [InlineData("http://localhost:5168/uploads/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("/uploads/../secret.png")]
    [InlineData("/uploads/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.svg")]
    [InlineData("/uploads/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.png")]
    [InlineData("/uploads/short.png")]
    [InlineData("")]
    public void AnythingButAnAddressWeHandedOut_IsRefused_EvenNextToAGoodOne(string bad)
    {
        Assert.False(IsValid(new[] { bad }));
        Assert.False(IsValid(new[] { Url('a'), bad }));
        Assert.False(IsValid(new[] { bad, Url('a') }));
    }

    [Fact]
    public void ANullEntry_IsRefused()
    {
        Assert.False(IsValid(new string?[] { null }));
    }

    [Theory]
    [InlineData("/uploads/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png")]   // a single string, not a list
    [InlineData(42)]
    public void ThingsThatAreNotAListOfAddresses_AreRefused(object value)
    {
        Assert.False(IsValid(value));
    }

    [Fact]
    public void TheMessage_SaysWhatIsAllowed()
    {
        var request = new PostRequest { Content = "x", MediaUrls = new[] { "https://example.com/pic.png" } };
        var results = new List<ValidationResult>();

        var ok = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        Assert.False(ok);
        var message = Assert.Single(results);
        Assert.Contains("at most 4 images", message.ErrorMessage);
        Assert.Contains(nameof(PostRequest.MediaUrls), message.MemberNames);
    }
}
