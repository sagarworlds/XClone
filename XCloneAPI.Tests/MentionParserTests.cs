using System.Text.Json;
using XCloneAPI.Services;
using XCloneAPI.Tests.Infrastructure;

namespace XCloneAPI.Tests;

/// <summary>Which words of a post are mentions (no server needed).</summary>
public class MentionParserTests
{
    private sealed record Case(string Text, string[] Names);

    private static List<Case> SharedCases()
    {
        using var document = JsonDocument.Parse(Frontend.Read("../testing/text-entities.json"));
        return document.RootElement.GetProperty("mentions").EnumerateArray()
            .Select(c => new Case(c.GetProperty("text").GetString()!, c.GetProperty("names").EnumerateArray().Select(t => t.GetString()!).ToArray()))
            .ToList();
    }

    [FactWithFrontend]
    public void EveryExampleThatTheAppIsTestedWith_GivesTheSameNamesHere()
    {
        var cases = SharedCases();
        Assert.True(cases.Count >= 40, $"expected the shared examples but found {cases.Count}");

        var wrong = cases
            .Where(c => !c.Names.SequenceEqual(MentionParser.Parse(c.Text)))
            .Select(c => $"\"{c.Text.Replace("\n", "\\n")}\": expected [{string.Join(", ", c.Names)}] but got [{string.Join(", ", MentionParser.Parse(c.Text))}]")
            .ToList();

        Assert.True(wrong.Count == 0, "The server and the app disagree about mentions:\n  " + string.Join("\n  ", wrong));
    }

    [Fact]
    public void NoText_HasNoMentions()
    {
        Assert.Empty(MentionParser.Parse(null));
        Assert.Empty(MentionParser.Parse(""));
    }

    [Fact]
    public void ANameOfFiftyCharacters_IsAMention_AndOneLongerIsNoneAtAll_NotACutOffOne()
    {
        var fifty = new string('a', 50);

        Assert.Equal(new[] { fifty }, MentionParser.Parse("@" + fifty));
        Assert.Empty(MentionParser.Parse("@" + fifty + "a"));
    }

    [Fact]
    public void OnlyTheFirstTenDifferentNames_Count()
    {
        var text = string.Join(" ", Enumerable.Range(1, 12).Select(i => $"@user{i:00}"));

        var names = MentionParser.Parse(text);

        Assert.Equal(Enumerable.Range(1, 10).Select(i => $"user{i:00}"), names);
        Assert.Equal(MentionParser.MaxMentions, names.Count);
    }

    [Fact]
    public void RepeatsInAnyCase_DoNotUseUpTheTen()
    {
        var text = string.Join(" ", Enumerable.Range(1, 30).Select(i => $"@Same @SAME @other{i % 9 + 1}"));

        var names = MentionParser.Parse(text);

        Assert.Equal(10, names.Count);   // "Same" plus the nine others
        Assert.Equal("Same", names[0]);  // spelled as it was first written
        Assert.Equal(names.Count, names.Select(n => n.ToLowerInvariant()).Distinct().Count());
    }

    [Fact]
    public void ANameIsKeptAsFirstWritten_NotLowerCased()
    {
        Assert.Equal(new[] { "BoB_Smith" }, MentionParser.Parse("@BoB_Smith @bob_smith"));
    }
}
