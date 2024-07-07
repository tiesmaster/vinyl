using System.Composition;

using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace Vinyl;

[Shared]
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RemoveCtorInjectedDependencyRefactoring))]
public class RemoveCtorInjectedDependencyRefactoring : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        var node = root.FindNode(context.Span);
        var query = new RemoveDependencyQuery(context.Document, root);

        // See if this is a parameter
        var parameterNode = node.FirstAncestorOrSelf<ParameterSyntax>();
        if (parameterNode is ParameterSyntax
            && query.FromParameter(parameterNode) is QueryResult queryResult
            && queryResult.CanApplyRefactoring)
        {
            context.RegisterRefactoring(queryResult.Command);
            return;
        }
    }

    private class RemoveDependencyQuery(Document document, SyntaxNode root)
    {
        public QueryResult FromParameter(ParameterSyntax parameterNode)
        {
            // The dependeny should be an parameter of a contructor
            var ctorNode = parameterNode.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
            if (ctorNode is null)
            {
                return QueryResult.NoResult;
            }

            // And the contructor should use the dependency to set a field
            var assignmentStatement = ctorNode
                .DescendantNodes()
                .OfType<ExpressionStatementSyntax>()
                .FirstOrDefault(x => IsFieldInitializerOfParameter(x, parameterNode));

            if (assignmentStatement is null)
            {
                return QueryResult.NoResult;
            }

            var fieldDeclaration = ctorNode
                .Ancestors()
                .OfType<ClassDeclarationSyntax>()
                .First()
                .DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .First();

            return QueryResult.Success(
                new RemoveDependencyCommand(
                    document: document,
                    root: root,
                    parameterNode: parameterNode,
                    statementNode: assignmentStatement,
                    fieldDeclaration: fieldDeclaration));
        }

        private static bool IsFieldInitializerOfParameter(ExpressionStatementSyntax expressionNode, ParameterSyntax parameterNode)
        {
            return expressionNode.Expression is AssignmentExpressionSyntax assignNode
                && assignNode.Kind() == SyntaxKind.SimpleAssignmentExpression
                && assignNode.Right is IdentifierNameSyntax rhIdent
                && rhIdent.Identifier.IsEquivalentTo(parameterNode.Identifier);
        }
    }

    private record QueryResult(
        bool CanApplyRefactoring,
        CodeAction Command)
    {
        public static QueryResult Success(CodeAction command)
            => new(
                CanApplyRefactoring: true,
                Command: command);

        public static QueryResult NoResult { get; } = new(
            CanApplyRefactoring: false,
            Command: null!);
    }

    private class RemoveDependencyCommand(
        Document document,
        SyntaxNode root,
        ParameterSyntax parameterNode,
        StatementSyntax statementNode,
        FieldDeclarationSyntax fieldDeclaration) : CodeAction
    {
        public override string Title => $"Remove dependency '{parameterNode.Identifier.ValueText}'";

        protected override Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
        {
            var newRoot = root.RemoveNodes([parameterNode, statementNode, fieldDeclaration], SyntaxRemoveOptions.KeepNoTrivia);

            return Task.FromResult(document.WithSyntaxRoot(newRoot!));
        }
    }
}