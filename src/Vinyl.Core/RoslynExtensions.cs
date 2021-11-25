using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace Microsoft.CodeAnalysis.CSharp.Syntax;

public static class RoslynExtensions
{
    public static SyntaxList<TNode> ToSyntaxList<TNode>(this IEnumerable<TNode> source)
        where TNode : SyntaxNode
        => SyntaxFactory.List(source);

    public static SeparatedSyntaxList<TNode> ToSeparatedSyntaxList<TNode>(this IEnumerable<TNode> source)
        where TNode : SyntaxNode
        => SyntaxFactory.SeparatedList(source);

    public static PropertyDeclarationSyntax WithModifiers(
        this PropertyDeclarationSyntax propertyNode,
        params SyntaxToken[] modifiers)
        => propertyNode.WithModifiers(SyntaxFactory.TokenList(modifiers));

    [SuppressMessage(
        "Design",
        "MA0016:Prefer return collection abstraction instead of implementation",
        Justification = "The purpose of this method is to return a concrete implementation, and not an abstraction.")]
    public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        => new(source);

    [SuppressMessage(
        "Design",
        "MA0016:Prefer return collection abstraction instead of implementation",
        Justification = "This is a special case, and we don't want to return an abstraction.")]
    public static HashSet<string> ToParameterNames(this ParameterListSyntax parameterList)
        => parameterList.Parameters
            .Select(node => node.Identifier.Text)
            .ToHashSet();

    [SuppressMessage(
        "Performance",
        "MA0078:Use 'Cast' instead of 'Select' to cast",
        Justification = "Not possible with linq comprehension syntax.")]
    public static IEnumerable<ConstructorDeclarationSyntax> GetContructors(
        this SyntaxList<MemberDeclarationSyntax> members)
        => from member in members
           where member.IsKind(SyntaxKind.ConstructorDeclaration)
           select (ConstructorDeclarationSyntax)member;

    public static TNode WithAnnotationsFrom<TNode>(this TNode node, SyntaxNode sourceNodeWithAnnotations)
        where TNode : SyntaxNode
        => sourceNodeWithAnnotations.CopyAnnotationsTo(node)!;

    public static SyntaxToken WithAnnotationsFrom(this SyntaxToken token, SyntaxToken sourceTokenWithAnnotations)
        => sourceTokenWithAnnotations.CopyAnnotationsTo(token);

    public static string ToPascalCase(this ISymbol symbol) => ToPascalCase(symbol.Name);

    public static string ToPascalCase(this string name)
        => char.ToUpper(name[1], CultureInfo.InvariantCulture) + name.Substring(2);

    public static string ToCamelCase(this string name)
        => char.ToLower(name[0], CultureInfo.InvariantCulture) + name.Substring(1);
}