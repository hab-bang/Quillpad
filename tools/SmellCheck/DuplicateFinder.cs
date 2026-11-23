using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static SmellCheck.SyntaxHelpers;

namespace SmellCheck;

/// <summary>
/// Finds code that is the same after names and literals are normalized: whole method bodies,
/// and runs of statements. Variable names become v0, v1... in the order they show up, and every
/// literal becomes the same token. A variable is a local or parameter of the member, or any other
/// simple name that starts with a lower-case letter or '_', such as a field or a parameter of the
/// method the code was copied from. Member, type and method names stay, so two blocks only match
/// when they call the same things.
/// </summary>
public static class DuplicateFinder
{
    private sealed record Fragment(string File, TextSpan Span, int Line, string Member, string Key);

    public static List<Finding> Find(IEnumerable<SyntaxTree> trees, Limits? limits = null)
    {
        limits ??= Limits.Default;
        var fragments = new List<Fragment>();
        foreach (var tree in trees) Collect(tree, limits, fragments);

        var duplicates = new List<(Fragment Fragment, Fragment Other)>();
        foreach (var group in fragments.GroupBy(f => f.Key).Where(g => g.Count() > 1))
        {
            var copies = group.ToList();
            foreach (var fragment in copies)
            {
                // Overlapping copies in one place, such as six equal lines in a row, are not a duplicate
                var other = copies.FirstOrDefault(o => !ReferenceEquals(o, fragment)
                    && !(o.File == fragment.File && o.Span.OverlapsWith(fragment.Span)));
                if (other != null) duplicates.Add((fragment, other));
            }
        }

        return Merge(duplicates);
    }

    // Overlapping duplicate fragments in one file make one finding.
    private static List<Finding> Merge(List<(Fragment Fragment, Fragment Other)> duplicates)
    {
        var findings = new List<Finding>();
        foreach (var file in duplicates.GroupBy(d => d.Fragment.File))
        {
            var regionEnd = -1;
            foreach (var (fragment, other) in file.OrderBy(d => d.Fragment.Span.Start).ThenByDescending(d => d.Fragment.Span.End))
            {
                if (fragment.Span.Start < regionEnd)
                {
                    regionEnd = Math.Max(regionEnd, fragment.Span.End);
                    continue;
                }

                regionEnd = fragment.Span.End;
                findings.Add(new Finding(Checks.Duplicate, fragment.File, fragment.Line, fragment.Member,
                    $"same code as {other.File}:{other.Line} ({other.Member})"));
            }
        }

        return findings;
    }

    private static void Collect(SyntaxTree tree, Limits limits, List<Fragment> fragments)
    {
        foreach (var node in tree.GetRoot().DescendantNodes())
        {
            switch (node)
            {
                case BlockSyntax block:
                    AddRuns(block.Statements, limits, fragments);
                    if (block.Parent is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax
                        && block.Statements.Sum(Weight) >= limits.MinDuplicateBodyStatements)
                    {
                        fragments.Add(MakeFragment(block.Statements, 0, block.Statements.Count - 1));
                    }
                    break;
                case SwitchSectionSyntax section:
                    AddRuns(section.Statements, limits, fragments);
                    break;
            }
        }
    }

    // For each start, the shortest run of statements that holds enough statements, nested ones included.
    private static void AddRuns(SyntaxList<StatementSyntax> statements, Limits limits, List<Fragment> fragments)
    {
        var weights = statements.Select(Weight).ToArray();
        for (var start = 0; start < statements.Count; start++)
        {
            var total = 0;
            var end = start;
            while (end < statements.Count && (total += weights[end]) < limits.MinDuplicateRunStatements) end++;

            if (total < limits.MinDuplicateRunStatements) return;
            fragments.Add(MakeFragment(statements, start, end));
        }
    }

    private static int Weight(StatementSyntax statement) =>
        statement.DescendantNodesAndSelf().Count(n => n is StatementSyntax and not BlockSyntax);

    private static Fragment MakeFragment(SyntaxList<StatementSyntax> statements, int start, int end)
    {
        var first = statements[start];
        var span = TextSpan.FromBounds(first.SpanStart, statements[end].Span.End);
        var key = Hash(Normalize(statements, start, end));
        return new Fragment(first.SyntaxTree.FilePath, span, LineOf(first), MemberOf(first), key);
    }

    private static string Normalize(SyntaxList<StatementSyntax> statements, int start, int end)
    {
        var scope = statements[start].Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault()
            ?? (SyntaxNode)statements[start].SyntaxTree.GetRoot();
        var locals = LocalNames(scope);
        var renamed = new Dictionary<string, string>();
        var builder = new StringBuilder();

        for (var i = start; i <= end; i++)
        {
            foreach (var token in statements[i].DescendantTokens())
            {
                builder.Append(NormalizeToken(token, locals, renamed)).Append(' ');
            }
        }

        return builder.ToString();
    }

    private static string NormalizeToken(SyntaxToken token, HashSet<string> locals, Dictionary<string, string> renamed)
    {
        switch (token.Kind())
        {
            case SyntaxKind.StringLiteralToken:
            case SyntaxKind.Utf8StringLiteralToken:
            case SyntaxKind.SingleLineRawStringLiteralToken:
            case SyntaxKind.MultiLineRawStringLiteralToken:
            case SyntaxKind.InterpolatedStringTextToken:
            case SyntaxKind.NumericLiteralToken:
            case SyntaxKind.CharacterLiteralToken:
            case SyntaxKind.TrueKeyword:
            case SyntaxKind.FalseKeyword:
            case SyntaxKind.NullKeyword:
                return "#lit";
            case SyntaxKind.IdentifierToken when IsVariable(token, locals):
                if (!renamed.TryGetValue(token.ValueText, out var name))
                {
                    name = "#v" + renamed.Count;
                    renamed[token.ValueText] = name;
                }
                return name;
            default:
                return token.Text;
        }
    }

    private static bool IsVariable(SyntaxToken token, HashSet<string> locals) => token.Parent switch
    {
        IdentifierNameSyntax name => IsSimpleName(name)
            && (locals.Contains(token.ValueText) || HasVariableStyle(token.ValueText)),
        VariableDeclaratorSyntax or ParameterSyntax or ForEachStatementSyntax
            or CatchDeclarationSyntax or SingleVariableDesignationSyntax => true,
        _ => false,
    };

    // Not the member part of a.b, not a method called by its name, and not a named argument
    private static bool IsSimpleName(IdentifierNameSyntax name) => name.Parent switch
    {
        MemberAccessExpressionSyntax access => access.Expression == name,
        InvocationExpressionSyntax call => call.Expression != name,
        QualifiedNameSyntax or AliasQualifiedNameSyntax or NameColonSyntax => false,
        _ => true,
    };

    private static bool HasVariableStyle(string name) => name[0] == '_' || char.IsLower(name[0]);

    private static HashSet<string> LocalNames(SyntaxNode scope)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in scope.DescendantNodes())
        {
            var identifier = node switch
            {
                ParameterSyntax parameter => parameter.Identifier,
                VariableDeclaratorSyntax variable when variable.Parent?.Parent is not BaseFieldDeclarationSyntax => variable.Identifier,
                ForEachStatementSyntax loop => loop.Identifier,
                CatchDeclarationSyntax catchDeclaration => catchDeclaration.Identifier,
                SingleVariableDesignationSyntax designation => designation.Identifier,
                _ => default,
            };
            if (identifier.ValueText.Length > 0) names.Add(identifier.ValueText);
        }

        return names;
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
