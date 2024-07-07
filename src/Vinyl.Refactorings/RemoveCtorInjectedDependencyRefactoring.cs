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

        // See if this is a parameter
        var parameterNode = node.FirstAncestorOrSelf<ParameterSyntax>();
        if (parameterNode is null)
        {
            return;
        }

        var query = new RemoveDependencyQuery(context.Document, root).FromParameter(parameterNode);
        if (query.CanApplyRefactoring)
        {
            context.RegisterRefactoring(query.Command);
        }

        //var assignmentExpression = ctorNode
        //    .DescendantNodes()
        //    .OfType<AssignmentExpressionSyntax>()
        //    .Single(x =>
        //        x.Right is IdentifierNameSyntax parameterIdentifier &&
        //        parameterIdentifier.Identifier == parameterNode.Identifier);


                //(StatementSyntax)assignmentExpression.Parent));

        //// And contained as part of a constructor declaration
        //var ctorNode = parameterNode.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
        //if (ctorNode is null)
        //{
        //    return;
        //}

        //var (b, fieldIdentifier) = ctorNode.DescendantNodes().OfType<StatementSyntax>().Any(x => IsFieldInitializer(x, parameterNode.Identifier));

        //var action = CodeAction.Create(
        //    "Expand seeded with new value",
        //    c => ExpandSeededDataWithNewValueAsync(context.Document, (InvocationExpressionSyntax)dataSeedExpression.Parent!, c));

        //context.RegisterRefactoring(action);
    }

    private (bool, SyntaxNode) IsFieldInitializer(StatementSyntax x, SyntaxToken identifier)
    {
        throw new NotImplementedException();
    }

    private class RemoveDependencyQuery(Document document, SyntaxNode root)
    {
        public QueryResult FromParameter(ParameterSyntax parameterNode)
        {
            var ctorNode = parameterNode.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
            var assignmentStatement = ctorNode.Body.Statements.First();

            var fieldDeclaration = ctorNode
                .Ancestors()
                .OfType<ClassDeclarationSyntax>()
                .First()
                .DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .First();

            return new QueryResult(
                canApplyRefactoring: true,
                new RemoveDependencyCommand(
                    document: document,
                    root: root,
                    parameterNode: parameterNode,
                    statementNode: assignmentStatement,
                    fieldDeclaration: fieldDeclaration));
        }
    }

    // TODO: Make record

    private class QueryResult(
        bool canApplyRefactoring,
        CodeAction command)
    {
        public bool CanApplyRefactoring { get; } = canApplyRefactoring;

        public CodeAction Command { get; } = command;
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