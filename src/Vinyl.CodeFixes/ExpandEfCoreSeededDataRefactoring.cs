using System.Composition;

using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace Vinyl;

[Shared]
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(ExpandEfCoreSeededDataRefactoring))]
public class ExpandEfCoreSeededDataRefactoring : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync().ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        var dbContextClasses = root
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(x => x.Identifier.ValueText.EndsWith("DbContext", StringComparison.Ordinal));

        if (!dbContextClasses.Any())
        {
            return;
        }

        var firstDbContext = dbContextClasses.First();

        var modelCreatingMethod = firstDbContext
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .SingleOrDefault(x => x.Identifier.ValueText == "OnModelCreating");

        if (modelCreatingMethod is null)
        {
            return;
        }

        var dataSeedExpressions = modelCreatingMethod
            .DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(x => x.Name.ToString() == "HasData");

        if (!dataSeedExpressions.Any())
        {
            return;
        }

        foreach (var dataSeedExpression in dataSeedExpressions)
        {
            var action = CodeAction.Create(
               "Expand seeded with new value",
               c => ExpandSeededDataWithNewValueAsync(context.Document, (InvocationExpressionSyntax)dataSeedExpression.Parent, c));

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
                    return node.ReplaceToken(stringLiteralToken, SyntaxFactory.Literal(Guid.NewGuid().ToString()));
                }
                else
                {
                    return node.WithExpression(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(string.Empty)));
                }
            });

        return templateArgument.WithExpression(newObjectCreation);
    }
}

public static class HoiExtensions
{
    public static ArgumentListSyntax AddArgument(this ArgumentListSyntax argumentList, ArgumentSyntax newValueArgument)
    {
        var arguments = argumentList.Arguments;

        var separator = arguments.GetSeparators().First();
        var argumentsAndSeparatorsList = arguments.GetWithSeparators();

        argumentsAndSeparatorsList = argumentsAndSeparatorsList.Add(separator);
        argumentsAndSeparatorsList = argumentsAndSeparatorsList.Add(newValueArgument);

        var newArguments = SyntaxFactory.SeparatedList<ArgumentSyntax>(argumentsAndSeparatorsList);

        return argumentList.WithArguments(newArguments);
    }
}