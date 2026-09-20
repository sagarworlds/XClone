using System.Text.Json;
using XCloneAPI.Services;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>Which words of a post are hashtags (no server needed).</summary>
public class HashtagParserTests
{
    private sealed record Case(string Text, string[] Tags);

    private static List<Case> SharedCases()
    {
        var json = Frontend.Read("../testing/text-entities.json");
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("hashtags").EnumerateArray()
            .Select(c => new Case(c.GetProperty("text").GetString()!, c.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!).ToArray()))
            .ToList();
    }

    [FactWithFrontend]
    public void EveryExampleThatTheAppIsTestedWith_GivesTheSameTagsHere()
    {
        var cases = SharedCases();
        Assert.True(cases.Count >= 40, $"expected the shared examples but found {cases.Count}");

        var wrong = cases
            .Where(c => !c.Tags.SequenceEqual(HashtagParser.Parse(c.Text)))
            .Select(c => $"\"{c.Text.Replace("\n", "\\n")}\": expected [{string.Join(", ", c.Tags)}] but got [{string.Join(", ", HashtagParser.Parse(c.Text))}]")
            .ToList();

        Assert.True(wrong.Count == 0, "The server and the app disagree about hashtags:\n  " + string.Join("\n  ", wrong));
    }

    [Fact]
    public void NoText_HasNoTags()
    {
        Assert.Empty(HashtagParser.Parse(null));
        Assert.Empty(HashtagParser.Parse(""));
    }

    [Fact]
    public void AFiftyLetterTag_IsATag_AndOneLongerIsNoTagAtAll_NotACutOffOne()
    {
        var fifty = new string('a', 50);

        Assert.Equal(new[] { fifty }, HashtagParser.Parse("#" + fifty));
        Assert.Empty(HashtagParser.Parse("#" + fifty + "a"));
        Assert.Equal(new[] { "ok" }, HashtagParser.Parse("#" + fifty + "a #ok"));
    }

    [Fact]
    public void OnlyTheFirstTenDifferentTags_Count()
    {
        var text = string.Join(" ", Enumerable.Range(1, 12).Select(i => $"#tag{i}"));

        var tags = HashtagParser.Parse(text);

        Assert.Equal(Enumerable.Range(1, 10).Select(i => $"tag{i}"), tags);
        Assert.Equal(HashtagParser.MaxTags, tags.Count);
    }

    [Fact]
    public void RepeatsDoNotUseUpTheTen()
    {
        var text = string.Join(" ", Enumerable.Range(1, 30).Select(i => $"#same #other{i % 9 + 1}"));

        var tags = HashtagParser.Parse(text);

        Assert.Equal(10, tags.Count);   // "same" plus the nine others
        Assert.Equal(tags.Count, tags.Distinct().Count());
        Assert.Equal("same", tags[0]);
    }

    [Fact]
    public void TagsAreLowerCased_TheSameWayForEveryone()
    {
        Assert.Equal(new[] { "hello" }, HashtagParser.Parse("#HELLO #Hello #hello"));
        Assert.Equal(new[] { "école" }, HashtagParser.Parse("#ÉCOLE"));
    }

    // ---- a tag as it is written in an address -----------------------------------------------------------------

    [Theory]
    [InlineData("sunset", "sunset")]
    [InlineData("Sunset", "sunset")]
    [InlineData("#Sunset", "sunset")]
    [InlineData("  #Sunset  ", "sunset")]
    [InlineData("tag\n", "tag")]
    [InlineData("SUNSET_2026", "sunset_2026")]
    [InlineData("2026a", "2026a")]
    [InlineData("ÉCOLE", "école")]
    [InlineData("日本語", "日本語")]
    public void ANameThatCouldBeATag_IsNormalised(string input, string expected)
    {
        Assert.True(HashtagParser.TryNormalize(input, out var tag));
        Assert.Equal(expected, tag);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("##a")]
    [InlineData("a b")]
    [InlineData("a-b")]
    [InlineData("a#b")]
    [InlineData("2026")]
    [InlineData("_")]
    [InlineData("two\nwords")]
    [InlineData("tag,")]
    [InlineData("😀")]
    [InlineData("../x")]
    public void AnythingElse_IsNotATag(string? input)
    {
        Assert.False(HashtagParser.TryNormalize(input, out var tag));
        Assert.Equal("", tag);
    }

    [Fact]
    public void TheLongestTag_Normalises_AndOneLongerDoesNot()
    {
        Assert.True(HashtagParser.TryNormalize(new string('a', 50), out _));
        Assert.False(HashtagParser.TryNormalize(new string('a', 51), out _));
    }
}
