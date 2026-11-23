using Xunit;

namespace SmellCheck.Tests;

public sealed class ReportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "smellcheck-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Write(string path, string text)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    [Theory]
    [InlineData("fix: keep the caret", true)]
    [InlineData("fix(editor): keep the caret", true)]
    [InlineData("Fix: keep the caret", true)]
    [InlineData("feat: add a button", false)]
    [InlineData("chore: fix the build", false)]
    [InlineData(null, false)]
    public void KnowsAFixTitle(string? title, bool isFix)
    {
        Assert.Equal(isFix, Reports.IsFixTitle(title));
    }

    [Theory]
    [InlineData(50, 10, false)]
    [InlineData(51, 10, true)]
    [InlineData(1, 0, true)]
    [InlineData(0, 0, false)]
    public void WarnsAboveFiveAddedLinesForEachRemovedLine(int added, int removed, bool warn)
    {
        Assert.Equal(warn, Reports.IsFixRatioTooHigh(added, removed));
    }

    [Fact]
    public void CountsChangedLinesWithoutBinaryFilesOrTheBaseline()
    {
        const string numstat = "10\t2\tsrc/A.cs\n-\t-\timage.png\n400\t3\tsmell-baseline.json\n5\t1\tREADME.md\n";
        Assert.Equal((15, 3), Reports.CountChangedLines(numstat));
    }

    [Fact]
    public void FindsTestsWithoutAssert()
    {
        var tests = Reports.FindTestsWithoutAssert("NoAssert.cs", Samples.Read("NoAssert.cs"));

        var test = Assert.Single(tests);
        Assert.Contains("NoAssert.ChecksNothing", test);
        Assert.StartsWith("NoAssert.cs:7 ", test);
    }

    [Fact]
    public void ReadsTheFilesATestProjectCompiles()
    {
        Write("src/App/Utilities/Parser.cs", "");
        Write("src/App/Utilities/Other.cs", "");
        Write("src/App/Views/Page.xaml.cs", "");
        Write("src/Lib/Markdown/Blocks/Block.cs", "");
        Write("src/Lib/Markdown/Render/Renderer.cs", "");
        Write("tests/T/T.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AppSource>..\..\src\App\</AppSource>
                <LibSource>..\..\src\Lib\Markdown\</LibSource>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="$(AppSource)Utilities\Parser.cs" Link="Linked\Parser.cs" />
                <Compile Include="$(LibSource)**\*.cs" Exclude="$(LibSource)Render\**" />
              </ItemGroup>
            </Project>
            """);

        var compiled = TestProjectFiles.Compiled(_root, "tests/T/T.csproj");

        Assert.Equal(new[] { "src/App/Utilities/Parser.cs", "src/Lib/Markdown/Blocks/Block.cs" }, compiled.OrderBy(f => f));
    }

    [Fact]
    public void ReadsTheRealTestProject()
    {
        var root = FindRepositoryRoot();
        if (root == null) return; // Only when the tests run inside the repository

        var compiled = TestProjectFiles.Compiled(root, Reports.TestProject);

        Assert.Contains("src/Notepads/Utilities/WordCountUtility.cs", compiled);
        Assert.Contains("src/Notepads.Controls/MarkdownTextBlock/Markdown/MarkdownDocument.cs", compiled);
        Assert.DoesNotContain("src/Notepads/Core/SessionManager.cs", compiled);
        Assert.DoesNotContain(compiled, f => f.Contains("/Markdown/Render/"));
    }

    private static string? FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, Reports.TestProject))) return dir.FullName;
        }

        return null;
    }
}
