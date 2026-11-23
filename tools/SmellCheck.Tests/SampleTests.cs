using System.Text.RegularExpressions;
using Xunit;

namespace SmellCheck.Tests;

/// <summary>
/// Runs each sample and compares the counts with the "expect" line at the top of the sample.
/// </summary>
public class SampleTests
{
    private static readonly Limits SmallLimits = Limits.Default with { MaxMethodLines = 10, MaxFileLines = 10 };

    [Theory]
    [InlineData("Nesting.cs")]
    [InlineData("MethodLength.cs")]
    [InlineData("Parameters.cs")]
    [InlineData("FileLength.cs")]
    [InlineData("Duplicate.cs")]
    [InlineData("CatchAll.cs")]
    [InlineData("EmptyCatch.cs")]
    [InlineData("CommentRun.cs")]
    [InlineData("Delay.cs")]
    [InlineData("Suppression.cs")]
    [InlineData("Suppression.csproj")]
    public void SampleGivesTheExpectedCounts(string sample)
    {
        var text = Samples.Read(sample);
        var findings = Samples.Analyze(sample, text, SmallLimits);

        foreach (var (check, expected) in Samples.Expectations(text))
        {
            var actual = findings.Where(f => f.Check == check).ToList();
            Assert.True(expected == actual.Count,
                $"{sample}: expected {expected} {check}, got {actual.Count}:\n" + string.Join("\n", actual));
        }
    }

    [Fact]
    public void EverySampleSaysWhatItExpects()
    {
        foreach (var file in Directory.GetFiles(Samples.Folder))
        {
            Assert.NotEmpty(Samples.Expectations(File.ReadAllText(file)));
        }
    }

    [Fact]
    public void FileLengthFindingHoldsTheLineCount()
    {
        var finding = Assert.Single(Samples.Analyze("FileLength.cs", Samples.Read("FileLength.cs"), SmallLimits),
            f => f.Check == Checks.FileLength);
        Assert.Equal(14, finding.Value);
    }

    [Fact]
    public void FindingNamesTheFileTheMemberAndTheLine()
    {
        var finding = Samples.Analyze("Nesting.cs", Samples.Read("Nesting.cs"), SmallLimits)
            .First(f => f.Check == Checks.Nesting);
        Assert.Equal("Nesting.cs", finding.File);
        Assert.Equal("Nesting.TooDeep", finding.Member);
        Assert.Equal(18, finding.Line);
    }

    [Fact]
    public void DefaultLimitsMatchTheIssue()
    {
        var limits = Limits.Default;
        Assert.Equal(4, limits.MaxNesting);
        Assert.Equal(60, limits.MaxMethodLines);
        Assert.Equal(5, limits.MaxParameters);
        Assert.Equal(1, limits.MaxBoolParameters);
        Assert.Equal(500, limits.MaxFileLines);
        Assert.Equal(5, limits.MinDuplicateRunStatements);
        Assert.Equal(3, limits.MinCommentRunLines);
    }

    [Fact]
    public void FileOfFiveHundredLinesIsFineAndOneMoreIsNot()
    {
        string Source(int lines) => "class A {\n" + string.Concat(Enumerable.Repeat("\n", lines - 2)) + "}\n";

        Assert.DoesNotContain(Samples.Analyze("A.cs", Source(500), Limits.Default), f => f.Check == Checks.FileLength);
        Assert.Contains(Samples.Analyze("A.cs", Source(501), Limits.Default), f => f.Check == Checks.FileLength);
    }

    [Fact]
    public void DuplicatesAreFoundAcrossFiles()
    {
        const string body = "void M(int a) { var b = a + 1; Use(b); Use(a); Use(b + 2); Use(3); }";
        var trees = new[]
        {
            SyntaxHelpers.Parse("One.cs", "class One { " + body + " }"),
            SyntaxHelpers.Parse("Two.cs", "class Two { void N(int x) { var y = x + 1; Use(y); Use(x); Use(y + 2); Use(4); } }"),
        };

        var findings = DuplicateFinder.Find(trees);

        Assert.Equal(new[] { "One.cs", "Two.cs" }, findings.Select(f => f.File).OrderBy(f => f));
        Assert.Contains("Two.cs", findings.First(f => f.File == "One.cs").Message);
    }
}

internal static class Samples
{
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Samples");

    public static string Read(string name) => File.ReadAllText(Path.Combine(Folder, name));

    public static List<Finding> Analyze(string name, string text, Limits limits)
    {
        if (name.EndsWith(".csproj", StringComparison.Ordinal)) return ProjectFileChecks.Run(name, text);

        var tree = SyntaxHelpers.Parse(name, text);
        return FileChecks.Run(tree, limits).Concat(DuplicateFinder.Find(new[] { tree }, limits)).ToList();
    }

    /// <summary>Reads "expect catch-all: 3, empty-catch: 0" from the first lines of a sample.</summary>
    public static List<(string Check, int Count)> Expectations(string text)
    {
        var line = text.Split('\n').Take(2).FirstOrDefault(l => l.Contains("expect ")) ?? "";
        return Regex.Matches(line, @"([A-Za-z][A-Za-z -]*?): (\d+)")
            .Select(m => (m.Groups[1].Value.Replace("expect ", "").Trim(), int.Parse(m.Groups[2].Value)))
            .ToList();
    }
}
