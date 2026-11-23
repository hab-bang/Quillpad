using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static SmellCheck.SyntaxHelpers;

namespace SmellCheck;

/// <summary>The checks that look at one .cs file at a time. Duplicates are in DuplicateFinder.</summary>
public static class FileChecks
{
    public static List<Finding> Run(SyntaxTree tree, Limits? limits = null)
    {
        limits ??= Limits.Default;
        var root = tree.GetRoot();
        var findings = new List<Finding>();

        CheckNesting(root, 0, limits, findings);
        CheckMembers(root, limits, findings);
        CheckCatches(root, findings);
        CheckDelays(root, findings);
        CheckComments(tree, limits, findings);
        CheckSuppressions(tree, findings);
        CheckFileLength(tree, limits, findings);
        return findings;
    }

    private static Finding At(string check, SyntaxNode node, string message) =>
        new(check, node.SyntaxTree.FilePath, LineOf(node), MemberOf(node), message);

    // Nesting: only statements open a level. Lambdas, local functions and initializers don't,
    // and an "else if" stays on the level of its "if".
    private static void CheckNesting(SyntaxNode node, int depth, Limits limits, List<Finding> findings)
    {
        foreach (var child in node.ChildNodes())
        {
            var childDepth = depth;
            if (OpensLevel(child))
            {
                childDepth++;
                if (childDepth == limits.MaxNesting + 1)
                {
                    findings.Add(At(Checks.Nesting, child,
                        $"nested {childDepth} levels deep (limit {limits.MaxNesting})"));
                }
            }

            CheckNesting(child, childDepth, limits, findings);
        }
    }

    private static bool OpensLevel(SyntaxNode node) => node switch
    {
        IfStatementSyntax ifStatement => ifStatement.Parent is not ElseClauseSyntax,
        ForStatementSyntax or CommonForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax
            or TryStatementSyntax or SwitchStatementSyntax or UsingStatementSyntax
            or LockStatementSyntax or FixedStatementSyntax => true,
        _ => false,
    };

    private static void CheckMembers(SyntaxNode root, Limits limits, List<Finding> findings)
    {
        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case BaseMethodDeclarationSyntax method:
                    CheckLength(method, method.Body ?? (SyntaxNode?)method.ExpressionBody, limits, findings);
                    CheckParameters(method, method.ParameterList, limits, findings);
                    break;
                case LocalFunctionStatementSyntax local:
                    CheckLength(local, local.Body ?? (SyntaxNode?)local.ExpressionBody, limits, findings);
                    CheckParameters(local, local.ParameterList, limits, findings);
                    break;
                case AccessorDeclarationSyntax accessor:
                    CheckLength(accessor, accessor.Body ?? (SyntaxNode?)accessor.ExpressionBody, limits, findings);
                    break;
                case DelegateDeclarationSyntax del:
                    CheckParameters(del, del.ParameterList, limits, findings);
                    break;
            }
        }
    }

    private static void CheckLength(SyntaxNode member, SyntaxNode? body, Limits limits, List<Finding> findings)
    {
        if (body == null) return;

        // Count from the first modifier or keyword, so attributes above the member don't count
        var start = member.ChildNodesAndTokens().First(c => !c.IsKind(SyntaxKind.AttributeList)).SpanStart;
        var tree = member.SyntaxTree;
        var lines = LineOf(tree, member.Span.End) - LineOf(tree, start) + 1;
        if (lines > limits.MaxMethodLines)
        {
            findings.Add(new Finding(Checks.MethodLength, tree.FilePath, LineOf(tree, start), MemberOf(member),
                $"{lines} lines long (limit {limits.MaxMethodLines})"));
        }
    }

    private static void CheckParameters(SyntaxNode member, ParameterListSyntax list, Limits limits, List<Finding> findings)
    {
        var count = list.Parameters.Count;
        if (count > limits.MaxParameters)
        {
            findings.Add(At(Checks.Parameters, member, $"{count} parameters (limit {limits.MaxParameters})"));
        }

        var bools = list.Parameters.Count(p => IsBool(p.Type));
        if (bools > limits.MaxBoolParameters)
        {
            findings.Add(At(Checks.Parameters, member, $"{bools} bool parameters (limit {limits.MaxBoolParameters})"));
        }
    }

    private static bool IsBool(TypeSyntax? type) => type switch
    {
        PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.BoolKeyword),
        NullableTypeSyntax nullable => IsBool(nullable.ElementType),
        NameSyntax name => LastName(name) == "Boolean",
        _ => false,
    };

    private static void CheckCatches(SyntaxNode root, List<Finding> findings)
    {
        foreach (var clause in root.DescendantNodes().OfType<CatchClauseSyntax>())
        {
            if (clause.Filter == null && clause.Declaration == null)
            {
                findings.Add(At(Checks.CatchAll, clause, "bare catch without a when filter"));
            }
            else if (clause.Filter == null && IsException(clause.Declaration!.Type))
            {
                findings.Add(At(Checks.CatchAll, clause, "catch (Exception) without a when filter"));
            }

            if (clause.Block.Statements.Count == 0)
            {
                findings.Add(At(Checks.EmptyCatch, clause, "empty catch block"));
            }
        }
    }

    private static bool IsException(TypeSyntax type) =>
        type.ToString() is "Exception" or "System.Exception" or "global::System.Exception";

    private static void CheckDelays(SyntaxNode root, List<Finding> findings)
    {
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not MemberAccessExpressionSyntax access) continue;

            var target = LastName(access.Expression);
            var method = access.Name.Identifier.Text;
            if ((target, method) is ("Task", "Delay") or ("Thread", "Sleep"))
            {
                findings.Add(At(Checks.Delay, call, $"{target}.{method}"));
            }
        }
    }

    // Runs of full-line // comments. /// docs are a different trivia kind, so they don't count.
    private static void CheckComments(SyntaxTree tree, Limits limits, List<Finding> findings)
    {
        var root = tree.GetRoot();
        var text = tree.GetText();
        var commentLines = new List<(int Line, int Position)>();

        // The license header above the first using or namespace is not counted
        var headerEnd = root.GetFirstToken(includeZeroWidth: true).SpanStart;

        foreach (var trivia in root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.SpanStart < headerEnd) continue;

            var line = text.Lines.GetLineFromPosition(trivia.SpanStart);
            var before = text.ToString(TextSpan.FromBounds(line.Start, trivia.SpanStart));
            if (before.Trim().Length == 0) commentLines.Add((line.LineNumber, trivia.SpanStart));
        }

        var runStart = 0;
        for (var i = 1; i <= commentLines.Count; i++)
        {
            if (i < commentLines.Count && commentLines[i].Line == commentLines[i - 1].Line + 1) continue;

            var length = i - runStart;
            if (length >= limits.MinCommentRunLines)
            {
                var first = commentLines[runStart];
                findings.Add(new Finding(Checks.CommentRun, tree.FilePath, first.Line + 1,
                    MemberAt(root, first.Position), $"{length} // comment lines in a row"));
            }

            runStart = i;
        }
    }

    private static void CheckSuppressions(SyntaxTree tree, List<Finding> findings)
    {
        var root = tree.GetRoot();

        foreach (var pragma in root.DescendantNodes(descendIntoTrivia: true).OfType<PragmaWarningDirectiveTriviaSyntax>())
        {
            if (pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword)
                && !HasReasonOnLine(root, pragma.SpanStart))
            {
                findings.Add(new Finding(Checks.Suppression, tree.FilePath, LineOf(tree, pragma.SpanStart),
                    MemberAt(root, pragma.SpanStart), "#pragma warning disable without a // reason on the same line"));
            }
        }

        foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
        {
            if (LastName(attribute.Name) is not ("SuppressMessage" or "SuppressMessageAttribute")) continue;
            if (HasJustification(attribute) || HasReasonOnLine(root, attribute.SpanStart)) continue;

            findings.Add(At(Checks.Suppression, attribute, "[SuppressMessage] without a Justification"));
        }
    }

    private static bool HasJustification(AttributeSyntax attribute) =>
        attribute.ArgumentList?.Arguments.Any(argument =>
            argument.NameEquals?.Name.Identifier.Text == "Justification"
            && argument.Expression is LiteralExpressionSyntax literal
            && literal.Token.ValueText.Trim().Length > 0) == true;

    private static void CheckFileLength(SyntaxTree tree, Limits limits, List<Finding> findings)
    {
        var lines = CountLines(tree.GetText());
        if (lines > limits.MaxFileLines)
        {
            findings.Add(new Finding(Checks.FileLength, tree.FilePath, limits.MaxFileLines + 1, "(file)",
                $"{lines} lines long (limit {limits.MaxFileLines})", lines));
        }
    }

    /// <summary>Lines as an editor shows them, without the empty line after a final line break.</summary>
    public static int CountLines(SourceText text)
    {
        var count = text.Lines.Count;
        return count > 0 && text.Lines[count - 1].Span.IsEmpty ? count - 1 : count;
    }
}
