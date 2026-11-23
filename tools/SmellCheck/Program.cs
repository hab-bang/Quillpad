using SmellCheck;

// Usage:
//   SmellCheck [--root <dir>]                 Fails when a count differs from smell-baseline.json
//   SmellCheck [--root <dir>] --update        Writes smell-baseline.json
//   SmellCheck [--root <dir>] --list          Prints every finding, known or new
//   SmellCheck [--root <dir>] --report --base <ref> [--title <PR title>]
//                                             Writes the reports for the job summary. Never fails.

var root = Directory.GetCurrentDirectory();
var update = false;
var report = false;
var list = false;
string baseRef = "origin/main";
string? title = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--root" when i + 1 < args.Length: root = Path.GetFullPath(args[++i]); break;
        case "--base" when i + 1 < args.Length: baseRef = args[++i]; break;
        case "--title" when i + 1 < args.Length: title = args[++i]; break;
        case "--update": update = true; break;
        case "--report": report = true; break;
        case "--list": list = true; break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 2;
    }
}

if (!Directory.Exists(Path.Combine(root, "src")))
{
    Console.Error.WriteLine($"No src folder in {root}. Run from the repository root or pass --root.");
    return 2;
}

if (report)
{
    return Cli.Report(root, baseRef, title);
}

if (list)
{
    foreach (var finding in Scanner.Scan(root).Findings) Console.WriteLine($"{finding.Check,-14} {finding}");
    return 0;
}

return update ? Cli.Update(root) : Cli.Check(root);

internal static class Cli
{
    private const string UpdateHint =
        "To record the new counts, run `make baseline` (or `dotnet run --project tools/SmellCheck -- --update`)\n" +
        "and commit smell-baseline.json. Say in the commit why a count went up.";

    public static int Check(string root)
    {
        var baselinePath = Path.Combine(root, Baseline.FileName);
        var baseline = Baseline.Load(baselinePath);
        if (baseline == null)
        {
            Console.Error.WriteLine($"No {Baseline.FileName}. " + UpdateHint);
            return 1;
        }

        var scan = Scanner.Scan(root);
        var problems = BaselineComparer.Compare(baseline, scan);
        if (problems.Count == 0)
        {
            Console.WriteLine($"Smell check passed: {scan.Findings.Count} known findings, no count changed.");
            return 0;
        }

        Print(problems);
        return 1;
    }

    private static void Print(List<Problem> problems)
    {
        var up = problems.Where(p => p.Kind != ProblemKind.WentDown).ToList();
        var down = problems.Where(p => p.Kind == ProblemKind.WentDown).ToList();

        if (up.Count > 0)
        {
            Console.WriteLine($"Smell check failed: {up.Count} count(s) went up.\n");
            foreach (var problem in up)
            {
                Console.WriteLine(problem.Describe());
                foreach (var finding in problem.Findings) Console.WriteLine($"  {finding}");
                Console.WriteLine();
            }
        }

        if (down.Count > 0)
        {
            Console.WriteLine($"{down.Count} count(s) went down. The baseline must go down with them:\n");
            foreach (var problem in down) Console.WriteLine(problem.Describe());
            Console.WriteLine();
        }

        Console.WriteLine(UpdateHint);
    }

    public static int Update(string root)
    {
        var path = Path.Combine(root, Baseline.FileName);
        var old = Baseline.Load(path) ?? new Baseline();
        var scan = Scanner.Scan(root);
        var changes = BaselineComparer.Compare(old, scan);

        Baseline.From(scan).Save(path);
        Console.WriteLine($"Wrote {Baseline.FileName}: {scan.Findings.Count} findings.");
        foreach (var change in changes) Console.WriteLine($"  {change.Describe()}");

        foreach (var total in scan.Findings.GroupBy(f => f.Check).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {total.Key}: {total.Count()}");
        }

        return 0;
    }

    public static int Report(string root, string baseRef, string? title)
    {
        var text = Reports.Build(root, baseRef, title);
        Console.WriteLine(text);

        var summary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (!string.IsNullOrEmpty(summary)) File.AppendAllText(summary, text);
        return 0;
    }
}
