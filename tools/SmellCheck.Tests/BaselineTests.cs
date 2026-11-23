using Xunit;

namespace SmellCheck.Tests;

public class BaselineTests
{
    private const string Hash = "sha256:abc";

    private static ScanResult Scan(params Finding[] findings) =>
        new(findings.ToList(), Counts.From(findings), Hash);

    private static Finding CatchAll(string file, int line) =>
        new(Checks.CatchAll, file, line, "A.M", "catch (Exception) without a when filter");

    private static Baseline BaselineOf(params Finding[] findings) => Baseline.From(Scan(findings));

    [Fact]
    public void SameCountsPass()
    {
        var baseline = BaselineOf(CatchAll("a.cs", 1));
        Assert.Empty(BaselineComparer.Compare(baseline, Scan(CatchAll("a.cs", 9))));
    }

    [Fact]
    public void HigherCountFailsAndListsTheFindings()
    {
        var baseline = BaselineOf(CatchAll("a.cs", 1));
        var problem = Assert.Single(BaselineComparer.Compare(baseline, Scan(CatchAll("a.cs", 1), CatchAll("a.cs", 7))));

        Assert.Equal(ProblemKind.WentUp, problem.Kind);
        Assert.Equal(2, problem.Findings.Count);
        Assert.Contains("went up from 1 to 2", problem.Describe());
    }

    [Fact]
    public void LowerCountFailsUntilTheBaselineIsUpdated()
    {
        var baseline = BaselineOf(CatchAll("a.cs", 1), CatchAll("a.cs", 7));
        var problem = Assert.Single(BaselineComparer.Compare(baseline, Scan(CatchAll("a.cs", 1))));

        Assert.Equal(ProblemKind.WentDown, problem.Kind);
        Assert.Contains("went down from 2 to 1", problem.Describe());
    }

    [Fact]
    public void MovingAFindingToAnotherFileFails()
    {
        var baseline = BaselineOf(CatchAll("a.cs", 1));
        var problems = BaselineComparer.Compare(baseline, Scan(CatchAll("b.cs", 1)));

        Assert.Contains(problems, p => p.File == "b.cs" && p.Kind == ProblemKind.WentUp);
        Assert.Contains(problems, p => p.File == "a.cs" && p.Kind == ProblemKind.WentDown);
    }

    [Fact]
    public void LongFileMayNotGrow()
    {
        Finding Length(int lines) => new(Checks.FileLength, "a.cs", 501, "(file)", "", lines);

        var problem = Assert.Single(BaselineComparer.Compare(BaselineOf(Length(700)), Scan(Length(701))));
        Assert.Equal(ProblemKind.WentUp, problem.Kind);
        Assert.Contains("grew from 700 to 701 lines", problem.Describe());
    }

    [Fact]
    public void ChangedEditorConfigFails()
    {
        var baseline = BaselineOf();
        baseline.EditorConfigHash = "sha256:other";

        var problem = Assert.Single(BaselineComparer.Compare(baseline, Scan()));
        Assert.Equal(ProblemKind.EditorConfigChanged, problem.Kind);
    }

    [Fact]
    public void BaselineRoundTripsThroughJson()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var baseline = BaselineOf(CatchAll("b.cs", 1), CatchAll("a.cs", 1), CatchAll("a.cs", 2));
            baseline.Save(path);
            var loaded = Baseline.Load(path)!;

            Assert.Equal(Hash, loaded.EditorConfigHash);
            Assert.Equal(2, loaded.Files.Get("a.cs", Checks.CatchAll));
            Assert.Empty(BaselineComparer.Compare(loaded, Scan(CatchAll("b.cs", 1), CatchAll("a.cs", 1), CatchAll("a.cs", 2))));

            var json = File.ReadAllText(path);
            Assert.DoesNotContain("\r", json);
            Assert.True(json.IndexOf("a.cs", StringComparison.Ordinal) < json.IndexOf("b.cs", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
