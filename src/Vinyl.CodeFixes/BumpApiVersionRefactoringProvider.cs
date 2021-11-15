using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Vinyl
{
    public class BumpApiVersionRefactoringProvider : CodeRefactoringProvider
    {
        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync();

            var attributeList = root.DescendantNodesAndSelf().OfType<AttributeListSyntax>().First();

            var attribute = attributeList.Attributes.First();
            if (attribute.Name is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == "ApiVersion")
            {
                var newVersion = CalculcateNewVersion(attribute);
                context.RegisterRefactoring(CodeAction.Create(
                    $"Bump API Version to {newVersion}",
                    c => BumpVersionAsync(context.Document, root, attributeList, newVersion, c)));
            }
        }

        private string CalculcateNewVersion(AttributeSyntax attribute)
        {
            var versionArgument = attribute.ArgumentList.Arguments.First();

            var versionArgumentExpression = (LiteralExpressionSyntax)versionArgument.Expression;
            var oldVersion = (string)versionArgumentExpression.Token.Value;

            var parsedOldVersion = new Version(oldVersion);
            var newVersion = new Version(parsedOldVersion.Major + 1, parsedOldVersion.Minor);

            return newVersion.ToString();
        }

        private Task<Document> BumpVersionAsync(
            Document document,
            SyntaxNode root,
            AttributeListSyntax oldApiVersionAttribute,
            string newVersion,
            CancellationToken arg)
        {
            var oldVersionToken = oldApiVersionAttribute.DescendantTokens().Single(x => x.IsKind(SyntaxKind.StringLiteralToken));

            var newApiVersionAttribute = oldApiVersionAttribute.ReplaceToken(oldVersionToken, SyntaxFactory.Literal(newVersion)).WithoutLeadingTrivia();
            var classDeclaration = (ClassDeclarationSyntax)oldApiVersionAttribute.Parent;
            var newClassDeclaration = classDeclaration.AddAttributeLists(newApiVersionAttribute);

            var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }
    }
}