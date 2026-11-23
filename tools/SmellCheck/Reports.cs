using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SmellCheck;

/// <summary>
/// Reports for the CI job summary. They never fail the build.
/// </summary>
public static class Reports
{
    public const string TestProject = "tests/Quillpad.Tests/Quillpad.Tests.csproj";
    public const int FixRatioLimit = 5;

    private static readonly string[] TestFolders = { "tests", "tools" };
    private static readonly string[] TestAttributes = { "Fact", "Theory", "Test", "TestCase", "TestMethod", "DataTestMethod" };

    public static string Build(string root, string baseRef, string? title)
    {
        var report = new StringBuilder("## Code quality reports\n\n");
        var mergeBase = Git(root, "merge-base", baseRef, "HEAD")?.Trim();
        if (string.IsNullOrEmpty(mergeBase))
        {
            report.Append($"Could not find `{baseRef}`, so the reports on changed lines and files are left out.\n\n");
        }
        else
        {
            AppendFixRatio(report, root, mergeBase, title);
            AppendUntestedFiles(report, root, mergeBase);
        }

        AppendTestsWithoutAssert(report, root);
        return report.ToString();
    }

    public static bool IsFixTitle(string? title) =>
        title != null && Regex.IsMatch(title, @"^fix(\([^)]*\))?!?:", RegexOptions.IgnoreCase);

    public static bool IsFixRatioTooHigh(int added, int removed) => added > FixRatioLimit * removed;

    private static void AppendFixRatio(StringBuilder report, string root, string mergeBase, string? title)
    {
        if (!IsFixTitle(title)) return;

        var (added, removed) = CountChangedLines(Git(root, "diff", "--numstat", mergeBase, "HEAD") ?? "");
        report.Append("### Lines in this fix\n\n");
        report.Append($"Added {added}, removed {removed}.\n\n");
        if (IsFixRatioTooHigh(added, removed))
        {
            report.Append($"> [!WARNING]\n> This fix adds more than {FixRatioLimit} lines for each line it removes. ")
                .Append("Check that it fixes the cause and doesn't only add code around it.\n\n");
        }
    }

    /// <summary>Sums `git diff --numstat` output. Binary files and the baseline don't count.</summary>
    public static (int Added, int Removed) CountChangedLines(string numstat)
    {
        int added = 0, removed = 0;
        foreach (var line in numstat.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 3 || parts[2].Trim() == Baseline.FileName) continue;
            if (int.TryParse(parts[0], out var a) && int.TryParse(parts[1], out var r))
            {
                added += a;
                removed += r;
            }
        }

        return (added, removed);
    }

    private static void AppendUntestedFiles(StringBuilder report, string root, string mergeBase)
    {
        var changed = (Git(root, "diff", "--name-only", "--diff-filter=AMR", mergeBase, "HEAD", "--", "src") ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (changed.Count == 0) return;

        var compiled = TestProjectFiles.Compiled(root, TestProject);
        var untested = changed.Where(f => !compiled.Contains(f)).ToList();

        report.Append("### Changed files the tests don't compile\n\n");
        if (untested.Count == 0)
        {
            report.Append($"All {changed.Count} changed `.cs` files are in `{TestProject}`.\n\n");
            return;
        }

        report.Append($"{untested.Count} of {changed.Count} changed `.cs` files are not in `{TestProject}`, ")
            .Append("so no unit test can reach them:\n\n");
        foreach (var file in untested) report.Append($"- `{file}`\n");
        report.Append('\n');
    }

    private static void AppendTestsWithoutAssert(StringBuilder report, string root)
    {
        var tests = TestFolders
            .SelectMany(folder => Scanner.EnumerateFiles(root, folder))
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !f.Contains("/Samples/"))
            .SelectMany(f => FindTestsWithoutAssert(f, File.ReadAllText(Path.Combine(root, f))))
            .ToList();

        report.Append("### Tests with no Assert\n\n");
        if (tests.Count == 0)
        {
            report.Append("None.\n");
            return;
        }

        foreach (var test in tests) report.Append($"- `{test}`\n");
    }

    /// <summary>Test methods whose body never names anything with "Assert" in it.</summary>
    public static List<string> FindTestsWithoutAssert(string file, string text)
    {
        var root = SyntaxHelpers.Parse(file, text).GetRoot();
        return root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(IsTest)
            .Where(m => !m.DescendantTokens().Any(t => t.ValueText.Contains("Assert", StringComparison.Ordinal)))
            .Select(m => $"{file}:{SyntaxHelpers.LineOf(m.SyntaxTree, m.Identifier.SpanStart)} {SyntaxHelpers.MemberOf(m)}")
            .ToList();
    }

    private static bool IsTest(MethodDeclarationSyntax method) =>
        method.AttributeLists.SelectMany(l => l.Attributes).Any(a =>
        {
            var name = SyntaxHelpers.LastName(a.Name) ?? "";
            if (name.EndsWith("Attribute", StringComparison.Ordinal)) name = name[..^"Attribute".Length];
            return TestAttributes.Contains(name);
        });

    private static string? Git(string root, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        try
        {
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null; // git is not installed
        }
    }
}

/// <summary>
/// The files a project compiles, read from its Compile items without MSBuild. It knows
/// $(Property) values set in the project, ';' lists, '*' and '**' wildcards, and Exclude.
/// </summary>
public static class TestProjectFiles
{
    public static HashSet<string> Compiled(string root, string project)
    {
        var path = Path.Combine(root, project);
        var compiled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return compiled;

        var document = XDocument.Load(path);
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.Descendants().Where(e => e.Parent?.Name.LocalName == "PropertyGroup"))
        {
            properties[property.Name.LocalName] = Expand(property.Value, properties);
        }

        var projectFolder = (Path.GetDirectoryName(project) ?? "").Replace('\\', '/');
        var allFiles = Scanner.EnumerateFiles(root, "src", skipExcludedFolders: false).ToList();
        foreach (var item in document.Descendants().Where(e => e.Name.LocalName == "Compile"))
        {
            var include = Patterns(item.Attribute("Include")?.Value, properties, projectFolder);
            var exclude = Patterns(item.Attribute("Exclude")?.Value, properties, projectFolder);
            foreach (var file in allFiles)
            {
                if (include.Any(p => p.IsMatch(file)) && !exclude.Any(p => p.IsMatch(file))) compiled.Add(file);
            }
        }

        return compiled;
    }

    private static string Expand(string value, Dictionary<string, string> properties) =>
        Regex.Replace(value, @"\$\(([^)]+)\)", m => properties.GetValueOrDefault(m.Groups[1].Value) ?? "");

    private static List<Regex> Patterns(string? value, Dictionary<string, string> properties, string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<Regex>();

        return Expand(value, properties)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => ToRegex(Resolve(projectFolder, p)))
            .ToList();
    }

    /// <summary>Joins a project-relative pattern to its folder and removes "." and ".." parts.</summary>
    public static string Resolve(string projectFolder, string pattern)
    {
        var parts = new List<string>();
        foreach (var part in (projectFolder + "/" + pattern.Replace('\\', '/')).Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == ".." && parts.Count > 0) parts.RemoveAt(parts.Count - 1);
            else parts.Add(part);
        }

        return string.Join("/", parts);
    }

    private static Regex ToRegex(string pattern)
    {
        var regex = Regex.Escape(pattern)
            .Replace(@"\*\*/", "(.*/)?")
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", "[^/]");
        return new Regex("^" + regex + "$", RegexOptions.IgnoreCase);
    }
}
