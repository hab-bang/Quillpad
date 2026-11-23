using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace SmellCheck;

/// <summary>Counts per file and per check: file path, then check name, then count.</summary>
public sealed class Counts : SortedDictionary<string, SortedDictionary<string, int>>
{
    public Counts() : base(StringComparer.Ordinal)
    {
    }

    public int Get(string file, string check) =>
        TryGetValue(file, out var checks) && checks.TryGetValue(check, out var count) ? count : 0;

    public static Counts From(IEnumerable<Finding> findings)
    {
        var counts = new Counts();
        foreach (var finding in findings)
        {
            if (!counts.TryGetValue(finding.File, out var checks))
            {
                checks = new SortedDictionary<string, int>(StringComparer.Ordinal);
                counts[finding.File] = checks;
            }

            checks[finding.Check] = checks.GetValueOrDefault(finding.Check) + finding.Value;
        }

        return counts;
    }
}

public sealed record ScanResult(List<Finding> Findings, Counts Counts, string EditorConfigHash);

/// <summary>Walks src/ and runs every check.</summary>
public static class Scanner
{
    public const string AnalyzerConfigFolder = "src";

    /// <summary>Files at the repository root that MSBuild reads for every project under src/.</summary>
    public static readonly string[] RootProjectFiles = { "Directory.Build.props", "Directory.Build.targets" };

    /// <summary>Vendored Windows Community Toolkit code that we don't change.</summary>
    public static readonly string[] ExcludedFolders = { "src/Notepads.Controls" };

    private static readonly HashSet<string> SkippedFolderNames =
        new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "AppPackages", "BundleArtifacts", ".vs" };

    private static readonly string[] ProjectFileExtensions = { ".csproj", ".props", ".targets" };

    public static ScanResult Scan(string root, Limits? limits = null)
    {
        var findings = new List<Finding>();
        var trees = new List<SyntaxTree>();

        foreach (var file in EnumerateFiles(root, "src"))
        {
            var text = File.ReadAllText(Path.Combine(root, file));
            if (file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                var tree = SyntaxHelpers.Parse(file, text);
                trees.Add(tree);
                findings.AddRange(FileChecks.Run(tree, limits));
            }
            else if (ProjectFileExtensions.Any(e => file.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            {
                findings.AddRange(ProjectFileChecks.Run(file, text));
            }
        }

        foreach (var file in RootProjectFiles.Where(f => File.Exists(Path.Combine(root, f))))
        {
            findings.AddRange(ProjectFileChecks.Run(file, File.ReadAllText(Path.Combine(root, file))));
        }

        findings.AddRange(DuplicateFinder.Find(trees, limits));
        findings = findings.OrderBy(f => f.File, StringComparer.Ordinal).ThenBy(f => f.Line).ToList();
        return new ScanResult(findings, Counts.From(findings), HashEditorConfig(root));
    }

    /// <summary>Files under a folder, as paths relative to root with forward slashes, in a fixed order.</summary>
    public static IEnumerable<string> EnumerateFiles(string root, string folder, bool skipExcludedFolders = true)
    {
        var start = Path.Combine(root, folder);
        if (!Directory.Exists(start)) return Enumerable.Empty<string>();

        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(start);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var sub in Directory.GetDirectories(directory))
            {
                var relative = Relative(root, sub);
                if (SkippedFolderNames.Contains(Path.GetFileName(sub))) continue;
                if (skipExcludedFolders && ExcludedFolders.Any(e => string.Equals(e, relative, StringComparison.OrdinalIgnoreCase))) continue;
                pending.Push(sub);
            }

            files.AddRange(Directory.GetFiles(directory).Select(f => Relative(root, f)));
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    public static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    /// <summary>
    /// A hash of every .editorconfig and .globalconfig under src/, with their paths, so a rule
    /// turned off in a new nested file changes it too. Line endings are made the same first,
    /// so Windows and Linux agree.
    /// </summary>
    public static string HashEditorConfig(string root)
    {
        var files = EnumerateFiles(root, AnalyzerConfigFolder, skipExcludedFolders: false)
            .Where(f => Path.GetFileName(f) is ".editorconfig" or ".globalconfig")
            .ToList();
        if (files.Count == 0) return "none";

        var text = new StringBuilder();
        foreach (var file in files)
        {
            text.Append(file).Append('\n')
                .Append(File.ReadAllText(Path.Combine(root, file)).Replace("\r\n", "\n")).Append('\n');
        }

        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }
}

/// <summary>NoWarn in project files. A reason is an XML comment with text in it on the same line.</summary>
public static class ProjectFileChecks
{
    public static List<Finding> Run(string file, string text)
    {
        var findings = new List<Finding>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            // "<NoWarn" also matches <NoWarn Condition="...">, and not the closing </NoWarn>
            if (lines[i].Contains("<NoWarn", StringComparison.OrdinalIgnoreCase)
                && !Regex.IsMatch(lines[i], @"<!--\s*[^\s-]"))
            {
                findings.Add(new Finding(Checks.Suppression, file, i + 1, "(project)",
                    "<NoWarn> without a <!-- reason --> on the same line"));
            }
        }

        return findings;
    }
}
