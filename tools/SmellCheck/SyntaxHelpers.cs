using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SmellCheck;

internal static class SyntaxHelpers
{
    private static readonly CSharpParseOptions ParseOptions =
        new(LanguageVersion.Preview, DocumentationMode.Parse);

    public static SyntaxTree Parse(string path, string text) =>
        CSharpSyntaxTree.ParseText(SourceText.From(text), ParseOptions, path);

    public static int LineOf(SyntaxTree tree, int position) =>
        tree.GetLineSpan(new TextSpan(position, 0)).StartLinePosition.Line + 1;

    public static int LineOf(SyntaxNode node) => LineOf(node.SyntaxTree, node.SpanStart);

    /// <summary>The containing type and member, such as "TextEditor.OnKeyDown.get".</summary>
    public static string MemberOf(SyntaxNode? node)
    {
        var parts = new List<string>();
        for (var current = node; current != null; current = current.Parent)
        {
            var name = NameOf(current);
            if (name != null) parts.Add(name);
        }

        parts.Reverse();
        return parts.Count == 0 ? "(file)" : string.Join(".", parts);
    }

    /// <summary>The member at a position, for things that are trivia, such as comments.</summary>
    public static string MemberAt(SyntaxNode root, int position) =>
        MemberOf(root.FindToken(position).Parent);

    private static string? NameOf(SyntaxNode node) => node switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier.Text,
        DelegateDeclarationSyntax del => del.Identifier.Text,
        MethodDeclarationSyntax method => method.Identifier.Text,
        ConstructorDeclarationSyntax => "ctor",
        DestructorDeclarationSyntax => "finalizer",
        OperatorDeclarationSyntax op => "operator " + op.OperatorToken.Text,
        ConversionOperatorDeclarationSyntax conv => "operator " + conv.Type,
        PropertyDeclarationSyntax property => property.Identifier.Text,
        EventDeclarationSyntax evt => evt.Identifier.Text,
        IndexerDeclarationSyntax => "this[]",
        AccessorDeclarationSyntax accessor => accessor.Keyword.Text,
        BaseFieldDeclarationSyntax field => string.Join(",", field.Declaration.Variables.Select(v => v.Identifier.Text)),
        LocalFunctionStatementSyntax local => local.Identifier.Text,
        EnumMemberDeclarationSyntax member => member.Identifier.Text,
        _ => null,
    };

    /// <summary>The last identifier of a name or member access: "Task" for System.Threading.Tasks.Task.</summary>
    public static string? LastName(SyntaxNode? node) => node switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        GenericNameSyntax generic => generic.Identifier.Text,
        QualifiedNameSyntax qualified => LastName(qualified.Right),
        AliasQualifiedNameSyntax alias => LastName(alias.Name),
        MemberAccessExpressionSyntax access => LastName(access.Name),
        _ => null,
    };

    /// <summary>True when the line that holds the position also has a // comment after it.</summary>
    public static bool HasCommentOnLine(SourceText text, int position)
    {
        var line = text.Lines.GetLineFromPosition(position);
        var rest = text.ToString(TextSpan.FromBounds(position, line.End));
        return rest.Contains("//", StringComparison.Ordinal);
    }
}
