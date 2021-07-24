using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public static class RoslynExtensions
    {
        public static SyntaxList<TNode> ToSyntaxList<TNode>(this IEnumerable<TNode> source) where TNode : SyntaxNode
            => SyntaxFactory.List(source);

        public static SeparatedSyntaxList<TNode> ToSeparatedSyntaxList<TNode>(this IEnumerable<TNode> source) where TNode : SyntaxNode
            => SyntaxFactory.SeparatedList(source);

        public static PropertyDeclarationSyntax WithModifiers(this PropertyDeclarationSyntax propertyNode, params SyntaxToken[] modifiers)
            => propertyNode.WithModifiers(SyntaxFactory.TokenList(modifiers));

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
            => new HashSet<T>(source);

        public static HashSet<string> ToParameterNames(this ParameterListSyntax parameterList)
            => parameterList.Parameters
                .Select(node => node.Identifier.Text)
                .ToHashSet();

        public static IEnumerable<ConstructorDeclarationSyntax> GetContructors(this SyntaxList<MemberDeclarationSyntax> members)
            => from member in members
               where member.IsKind(SyntaxKind.ConstructorDeclaration)
               select (ConstructorDeclarationSyntax)member;

        public static TNode WithAnnotationsFrom<TNode>(this TNode node, SyntaxNode sourceNodeWithAnnotations) where TNode : SyntaxNode
            => sourceNodeWithAnnotations.CopyAnnotationsTo(node);

        public static SyntaxToken WithAnnotationsFrom(this SyntaxToken token, SyntaxToken sourceTokenWithAnnotations)
            => sourceTokenWithAnnotations.CopyAnnotationsTo(token);
    }
}