using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmellCheck;

/// <summary>smell-baseline.json: the allowed counts per file and per check.</summary>
public sealed class Baseline
{
    public const string FileName = "smell-baseline.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [JsonPropertyName("editorconfig")]
    public string EditorConfigHash { get; set; } = "";

    [JsonPropertyName("files")]
    public Counts Files { get; set; } = new();

    public static Baseline From(ScanResult scan) =>
        new() { EditorConfigHash = scan.EditorConfigHash, Files = scan.Counts };

    public static Baseline? Load(string path)
    {
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<Baseline>(File.ReadAllText(path), JsonOptions) ?? new Baseline();
    }

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this, JsonOptions).Replace("\r\n", "\n") + "\n";
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }
}

public enum ProblemKind
{
    WentUp,
    WentDown,
    EditorConfigChanged,
}

public sealed record Problem(ProblemKind Kind, string File, string Check, int Allowed, int Now, IReadOnlyList<Finding> Findings)
{
    public string Describe() => Kind switch
    {
        ProblemKind.EditorConfigChanged =>
            $"{File} changed. A rule may have been turned off. If the change is meant, update the baseline.",
        ProblemKind.WentUp when Check == Checks.FileLength && Allowed == 0 =>
            $"{File}: {Now} lines, over the limit of 500.",
        ProblemKind.WentUp when Check == Checks.FileLength =>
            $"{File}: grew from {Allowed} to {Now} lines. Files over 500 lines may not grow.",
        ProblemKind.WentUp => $"{File}: {Check} went up from {Allowed} to {Now}.",
        ProblemKind.WentDown when Check == Checks.FileLength =>
            $"{File}: went down from {Allowed} to {Now} lines.",
        _ => $"{File}: {Check} went down from {Allowed} to {Now}.",
    };
}

public static class BaselineComparer
{
    /// <summary>
    /// Every count must equal the baseline. Higher is a new smell. Lower must be committed,
    /// so the baseline only goes down.
    /// </summary>
    public static List<Problem> Compare(Baseline baseline, ScanResult scan)
    {
        var problems = new List<Problem>();
        if (baseline.EditorConfigHash != scan.EditorConfigHash)
        {
            problems.Add(new Problem(ProblemKind.EditorConfigChanged, Scanner.EditorConfigPath, "", 0, 0, Array.Empty<Finding>()));
        }

        var files = baseline.Files.Keys.Union(scan.Counts.Keys).OrderBy(f => f, StringComparer.Ordinal);
        foreach (var file in files)
        {
            var checks = baseline.Files.GetValueOrDefault(file)?.Keys
                .Union(scan.Counts.GetValueOrDefault(file)?.Keys ?? Enumerable.Empty<string>())
                ?? scan.Counts[file].Keys;
            foreach (var check in checks.OrderBy(c => c, StringComparer.Ordinal))
            {
                var allowed = baseline.Files.Get(file, check);
                var now = scan.Counts.Get(file, check);
                if (allowed == now) continue;

                var findings = scan.Findings.Where(f => f.File == file && f.Check == check).ToList();
                problems.Add(new Problem(now > allowed ? ProblemKind.WentUp : ProblemKind.WentDown,
                    file, check, allowed, now, findings));
            }
        }

        return problems;
    }
}
