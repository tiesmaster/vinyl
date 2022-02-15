using System.Composition;
using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace Vinyl;

[Shared]
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(ExpandEfCoreSeededDataRefactoring))]
public class ExpandEfCoreSeededDataRefactoring : CodeRefactoringProvider
{
    [SuppressMessage(
        "StyleCop.CSharp.MaintainabilityRules",
        "SA1401:Fields should be private",
        Justification = "We need this for unit testing.")]
    [SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible", Justification = "See SA1401 suppression")]
    [SuppressMessage("Design", "MA0069:Non-constant static fields should not be visible", Justification = "See SA1401 suppression")]
    public static Func<Guid> NewGuidFactory = () => Guid.NewGuid();

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        var refactorNode = root.FindNode(context.Span);

        if (refactorNode is IdentifierNameSyntax dataSeedIdentifier && dataSeedIdentifier.Identifier.ValueText == "HasData")
        {
            var seededDataInvocation = dataSeedIdentifier.Ancestors().First(x => x is InvocationExpressionSyntax);
            var action = CodeAction.Create(
                "Expand seeded with new value",
                c => ExpandSeededDataWithNewValueAsync(context.Document, (InvocationExpressionSyntax)seededDataInvocation, c));

            context.RegisterRefactoring(action);
        }
    }

    private static async Task<Document> ExpandSeededDataWithNewValueAsync(
        Document document,
        InvocationExpressionSyntax seededDataInvocation,
        CancellationToken cancellationToken)
    {
        var root = await seededDataInvocation.SyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

        var argumentList = seededDataInvocation.ArgumentList;

        var newValueArgument = GenerateNewValue(argumentList.Arguments.Last());

        var newRoot = root.ReplaceNode(argumentList, argumentList.AddArgument(newValueArgument));

        return document.WithSyntaxRoot(newRoot);
    }

    [SuppressMessage("Performance", "EPS06:Hidden struct copy operation", Justification = "Limited impact")]
    private static ArgumentSyntax GenerateNewValue(ArgumentSyntax templateArgument)
    {
        var objectCreation = (AnonymousObjectCreationExpressionSyntax)templateArgument.Expression;

        var newObjectCreation = objectCreation.ReplaceNodes(
            objectCreation.Initializers.ToList(),
            (node, _) =>
            {
                if (node.NameEquals!.Name.Identifier.ValueText == "Id")
                {
                    var stringLiteralToken = node.DescendantTokens().Single(x => x.IsKind(SyntaxKind.StringLiteralToken));
                    return node.ReplaceToken(stringLiteralToken, SyntaxFactory.Literal(NewGuidFactory().ToString()));
                }

                return node.WithExpression(
                    SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(string.Empty)));
            });

        return templateArgument.WithExpression(newObjectCreation);
    }
}