using Xunit;

namespace SmellCheck.Tests;

/// <summary>Runs the scanner on a small repository made in a temp folder.</summary>
public sealed class ScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "smellcheck-" + Guid.NewGuid().ToString("N"));

    public ScannerTests()
    {
        Write("src/.editorconfig", "root = true\r\n[*.cs]\r\ndotnet_diagnostic.IDE0051.severity = warning\r\n");
        Write("src/App/Main.cs", "class Main { void M() { try { } catch { } } }");
        Write("src/App/obj/Generated.cs", "class Generated { void M() { try { } catch { } } }");
        Write("src/Notepads.Controls/Vendored.cs", "class Vendored { void M() { try { } catch { } } }");
        Write("src/App/App.csproj", "<Project><PropertyGroup><NoWarn>1591</NoWarn></PropertyGroup></Project>");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string path, string text)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    [Fact]
    public void ScansSourceButNotBuildOutputOrVendoredCode()
    {
        var scan = Scanner.Scan(_root);

        Assert.Equal(new[] { "src/App/App.csproj", "src/App/Main.cs" }, scan.Counts.Keys);
        Assert.Equal(1, scan.Counts.Get("src/App/Main.cs", Checks.CatchAll));
        Assert.Equal(1, scan.Counts.Get("src/App/Main.cs", Checks.EmptyCatch));
        Assert.Equal(1, scan.Counts.Get("src/App/App.csproj", Checks.Suppression));
    }

    [Fact]
    public void EditorConfigHashIgnoresLineEndingsButNotRules()
    {
        var crlf = Scanner.HashEditorConfig(_root);

        Write("src/.editorconfig", "root = true\n[*.cs]\ndotnet_diagnostic.IDE0051.severity = warning\n");
        Assert.Equal(crlf, Scanner.HashEditorConfig(_root));

        Write("src/.editorconfig", "root = true\n[*.cs]\ndotnet_diagnostic.IDE0051.severity = none\n");
        Assert.NotEqual(crlf, Scanner.HashEditorConfig(_root));
    }

    [Theory]
    [InlineData("src/App/.editorconfig", "[*.cs]\ndotnet_diagnostic.CA2242.severity = none\n")]
    [InlineData("src/App/.globalconfig", "is_global = true\ndotnet_diagnostic.CA2242.severity = none\n")]
    [InlineData("src/Notepads.Controls/.editorconfig", "[*.cs]\ndotnet_diagnostic.CA2242.severity = none\n")]
    public void NewNestedAnalyzerConfigChangesTheHash(string path, string text)
    {
        var before = Scanner.HashEditorConfig(_root);

        Write(path, text);

        Assert.NotEqual(before, Scanner.HashEditorConfig(_root));
    }

    [Fact]
    public void NestedAnalyzerConfigHashIgnoresLineEndings()
    {
        Write("src/App/.globalconfig", "is_global = true\r\ndotnet_diagnostic.CA2242.severity = none\r\n");
        var crlf = Scanner.HashEditorConfig(_root);

        Write("src/App/.globalconfig", "is_global = true\ndotnet_diagnostic.CA2242.severity = none\n");
        Assert.Equal(crlf, Scanner.HashEditorConfig(_root));
    }

    [Theory]
    [InlineData("Directory.Build.props")]
    [InlineData("Directory.Build.targets")]
    public void ScansProjectFilesAtTheRoot(string file)
    {
        Write(file, "<Project><PropertyGroup><NoWarn>$(NoWarn);CS0168</NoWarn></PropertyGroup></Project>");

        Assert.Equal(1, Scanner.Scan(_root).Counts.Get(file, Checks.Suppression));
    }

    [Fact]
    public void UpdatedBaselinePassesAndANewSmellFails()
    {
        var baselinePath = Path.Combine(_root, Baseline.FileName);
        Baseline.From(Scanner.Scan(_root)).Save(baselinePath);
        Assert.Empty(BaselineComparer.Compare(Baseline.Load(baselinePath)!, Scanner.Scan(_root)));

        Write("src/App/Main.cs", "class Main { void M() { try { } catch { } try { } catch { } } }");
        var problems = BaselineComparer.Compare(Baseline.Load(baselinePath)!, Scanner.Scan(_root));

        Assert.Equal(2, problems.Count);
        Assert.All(problems, p => Assert.Equal(ProblemKind.WentUp, p.Kind));
        Assert.All(problems, p => Assert.All(p.Findings, f => Assert.Equal("Main.M", f.Member)));
    }
}
