namespace SmellCheck;

/// <summary>The names of the checks, as they appear in smell-baseline.json.</summary>
public static class Checks
{
    public const string Nesting = "nesting";
    public const string MethodLength = "method-length";
    public const string Parameters = "parameters";
    public const string FileLength = "file-length";
    public const string Duplicate = "duplicate";
    public const string CatchAll = "catch-all";
    public const string EmptyCatch = "empty-catch";
    public const string CommentRun = "comment-run";
    public const string Delay = "delay";
    public const string Suppression = "suppression";
}

/// <summary>The thresholds. Tests can lower them to keep their samples small.</summary>
public sealed record Limits
{
    public static Limits Default { get; } = new();

    public int MaxNesting { get; init; } = 4;
    public int MaxMethodLines { get; init; } = 60;
    public int MaxParameters { get; init; } = 5;
    public int MaxBoolParameters { get; init; } = 1;
    public int MaxFileLines { get; init; } = 500;
    public int MinDuplicateRunStatements { get; init; } = 5;
    public int MinDuplicateBodyStatements { get; init; } = 3;
    public int MinCommentRunLines { get; init; } = 3;
}

/// <summary>
/// One smell at one place. Value is 1, except for file-length, where it is the line count:
/// a file that is already too long may not grow.
/// </summary>
public sealed record Finding(string Check, string File, int Line, string Member, string Message, int Value = 1)
{
    public override string ToString() => $"{File}:{Line} {Member}: {Message}";
}
