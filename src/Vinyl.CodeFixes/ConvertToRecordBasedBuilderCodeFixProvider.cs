using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Vinyl
{
    [Shared]
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConvertToRecordBasedBuilderCodeFixProvider))]
    public class ConvertToRecordBasedBuilderCodeFixProvider : CodeFixProvider
    {
        private const string _codeFixTitle = "Convert to record-based builder";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(ConvertToRecordBasedBuilderAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
            => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: _codeFixTitle,
                    createChangedSolution: c => ConvertToRecordBasedBuilder(context.Document, declaration, c),
                    equivalenceKey: _codeFixTitle),
                diagnostic);
        }

        [SuppressMessage("Performance", "EPS06:Hidden struct copy operation", Justification = "Limited impact")]
        private async Task<Solution> ConvertToRecordBasedBuilder(
            Document document,
            ClassDeclarationSyntax classDeclaration,
            CancellationToken cancellationToken)
        {
            // 1. Rename fields to be PascalCase
            // 2. Convert class -> record, and fields to parameter list
            // 3. Remove default ctor and default-setting ctor -> Default property
            // 4. Convert default with-er methods to make use of this with { Prop = ... };
            // 5. Use target-type new expression in Build() method

            SyntaxNode newRoot;
            ClassDeclarationSyntax newClassDeclaration;

            // ==================================================================================================================
            // Step 0: Paint target node
            // ==================================================================================================================

            newRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var targetNodeAnnotation = new SyntaxAnnotation();
            var newTargetNode = classDeclaration.WithAdditionalAnnotations(targetNodeAnnotation);

            newRoot = newRoot.ReplaceNode(classDeclaration, newTargetNode);

            // ==================================================================================================================
            // Step 1: Rename fields to be PascalCase
            // ==================================================================================================================

            newClassDeclaration = (ClassDeclarationSyntax)newRoot.GetAnnotatedNodes(targetNodeAnnotation).Single();

            var fieldNames = newClassDeclaration
                .Members
                .Where(x => x.IsKind(SyntaxKind.FieldDeclaration))
                .Cast<FieldDeclarationSyntax>()
                .Select(x => x.Declaration.Variables.Single().Identifier.ValueText)
                .ToHashSet();

            var tokensToRename = newRoot
                .DescendantTokens().Where(x => x.IsKind(SyntaxKind.IdentifierToken) && fieldNames.Contains(x.Text));

            newRoot = newRoot.ReplaceTokens(tokensToRename, (old, _)
                => SyntaxFactory.Identifier(old.Text.ToPascalCase()).WithTriviaFrom(old));

            // ==================================================================================================================
            // Step 2: Convert class -> record, and fields to parameter list
            // ==================================================================================================================

            newClassDeclaration = (ClassDeclarationSyntax)newRoot.GetAnnotatedNodes(targetNodeAnnotation).Single();

            var identifier = newClassDeclaration.Identifier;

            var newIdentifier = identifier.WithoutTrivia().WithLeadingTrivia(identifier.LeadingTrivia);

            var readonlyFields = newClassDeclaration
                .Members
                .Where(x => x.IsKind(SyntaxKind.FieldDeclaration))
                .Cast<FieldDeclarationSyntax>();

            var parameterList = ToParameterList(readonlyFields).WithTrailingTrivia(identifier.TrailingTrivia);

            var membersWithoutFields = newClassDeclaration
                .Members
                .Where(x => !x.IsKind(SyntaxKind.FieldDeclaration))
                .ToSyntaxList();

            var newRecordDeclaration = SyntaxFactory
                .RecordDeclaration(
                    newClassDeclaration.AttributeLists,
                    newClassDeclaration.Modifiers,
                    SyntaxFactory.Token(SyntaxKind.RecordKeyword),
                    newIdentifier,
                    newClassDeclaration.TypeParameterList,
                    parameterList,
                    newClassDeclaration.BaseList,
                    newClassDeclaration.ConstraintClauses,
                    newClassDeclaration.OpenBraceToken,
                    membersWithoutFields,
                    newClassDeclaration.CloseBraceToken,
                    default)
                .WithAnnotationsFrom(newClassDeclaration);

            // ==================================================================================================================
            // Step 3: Add default-setting ctor, and set defaults based on best matching constructor
            //         setting defaults, and remove all constructors
            // ==================================================================================================================

            var recordParameterNames = parameterList.ToParameterNames();

            // Find best matching default setting constructor and create default property with defaults from that constructor
            var bestMatchDefaultSettingContructor = FindBestMatchForDefaultSettingConstructor(newRecordDeclaration, recordParameterNames);

            var defaultValueLookup = CalculateDefaultValuesFromFieldSetting(
                (ConstructorDeclarationSyntax)bestMatchDefaultSettingContructor, recordParameterNames);

            var typeSyntax = SyntaxFactory.ParseTypeName(newRecordDeclaration.Identifier.Text);
            var defaultSettingProperty = SyntaxFactory
                .PropertyDeclaration(typeSyntax, "Default")
                .WithModifiers(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .WithExpressionBody(GetContructorInvocationFromParameterList(parameterList, defaultValueLookup))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

            newRecordDeclaration = newRecordDeclaration
                .WithMembers(newRecordDeclaration.Members.Insert(0, defaultSettingProperty));

            // Remove all constructors
            newRecordDeclaration = newRecordDeclaration.WithMembers(
                newRecordDeclaration
                    .Members
                    .Where(node => !node.IsKind(SyntaxKind.ConstructorDeclaration))
                    .ToSyntaxList());

            // ==================================================================================================================
            // Step 4: Convert default with-er methods to make use of this with { Prop = ... };
            // ==================================================================================================================

            var defaultWitherMethodNameToParametersMapping = newRecordDeclaration
                .ParameterList
                .Parameters
                .ToDictionary(
                    parameter => $"With{parameter.Identifier.ValueText}",
                    parameter => parameter);

            newRecordDeclaration = newRecordDeclaration.ReplaceNodes(
                newRecordDeclaration.Members.Where(member => IsDefaultWitherMethod(member, defaultWitherMethodNameToParametersMapping)),
                (node, _) =>
                {
                    var methodDeclaration = (MethodDeclarationSyntax)node;

                    var recordParameter = defaultWitherMethodNameToParametersMapping[methodDeclaration.Identifier.ValueText];

                    var propertyName = recordParameter.Identifier.ValueText;
                    var parameterName = methodDeclaration.ParameterList.Parameters.First().Identifier.ValueText;

                    return methodDeclaration.ReplaceNode(
                        methodDeclaration.ExpressionBody.Expression,
                        SyntaxFactory.ParseExpression($"this with {{ {propertyName} = {parameterName} }}"));
                });

            // ==================================================================================================================
            // Step 5: Use target-type new expression in Build() method
            // ==================================================================================================================

            static bool IsBuildMethod(MemberDeclarationSyntax member)
                => member is MethodDeclarationSyntax method && method.Identifier.ValueText == "Build";

            var buildMethod = (MethodDeclarationSyntax)newRecordDeclaration.Members.Single(IsBuildMethod);

            var oldNode = (ObjectCreationExpressionSyntax)buildMethod.ExpressionBody.Expression;

            var newNode = SyntaxFactory.ImplicitObjectCreationExpression().WithArgumentList(oldNode.ArgumentList);

            newRecordDeclaration = newRecordDeclaration.ReplaceNode(oldNode, newNode);

            // ==================================================================================================================
            // Final
            // ==================================================================================================================

            newRoot = newRoot.ReplaceNode(newClassDeclaration, newRecordDeclaration);
            return document.WithSyntaxRoot(newRoot).Project.Solution;
        }

        private static ParameterListSyntax ToParameterList(IEnumerable<FieldDeclarationSyntax> readonlyFields)
        {
            var parameters = readonlyFields.Select(x =>
            {
                var declaration = x.Declaration;
                return SyntaxFactory
                    .Parameter(declaration.Variables.First().Identifier)
                    .WithType(declaration.Type);
            });

            return SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters));
        }

        private static MemberDeclarationSyntax FindBestMatchForDefaultSettingConstructor(
            RecordDeclarationSyntax newRecordDeclaration,
            HashSet<string> recordParameterNames)
        {
            var allConstructors = newRecordDeclaration.Members.GetContructors();

            return allConstructors
                .OrderBy(node => GetDefaultSettingParameterAssignments(node, recordParameterNames).Count())
                .Last();
        }

        private static Dictionary<string, ExpressionSyntax> CalculateDefaultValuesFromFieldSetting(
            ConstructorDeclarationSyntax bestMatchDefaultSettingContructor,
            HashSet<string> recordParameterNames)
        {
            return GetDefaultSettingParameterAssignments(bestMatchDefaultSettingContructor, recordParameterNames)
                .ToDictionary(
                    assignment => ((IdentifierNameSyntax)assignment.Left).Identifier.Text,
                    assignment => assignment.Right);
        }

        private static IEnumerable<AssignmentExpressionSyntax> GetDefaultSettingParameterAssignments(
            ConstructorDeclarationSyntax contructor,
            HashSet<string> recordParameterNames)
        {
            var constructorParameterNames = contructor.ParameterList.ToParameterNames();

            bool IsFieldAssignment(ExpressionSyntax expression, HashSet<string> fields)
                => expression is IdentifierNameSyntax fieldSettingIdent
                    && fields.Contains(fieldSettingIdent.Identifier.Text);

            bool IsParameterReference(ExpressionSyntax expression, HashSet<string> parameters)
                => expression is IdentifierNameSyntax parameterIdent
                        && parameters.Contains(parameterIdent.Identifier.Text);

            return contructor.Body.Statements
                .Select(statement => (statement as ExpressionStatementSyntax)?.Expression as AssignmentExpressionSyntax)
                .Where(assignment => IsFieldAssignment(assignment.Left, recordParameterNames)
                                 && !IsParameterReference(assignment.Right, constructorParameterNames));
        }

        private ArrowExpressionClauseSyntax GetContructorInvocationFromParameterList(
            ParameterListSyntax parameterList,
            Dictionary<string, ExpressionSyntax> defaultValueLookup)
        {
            var fallbackDefaultValue = SyntaxFactory.ParseExpression("default");

            ArgumentSyntax ConvertParameterToArgumentWithDefault(ParameterSyntax parameter) => SyntaxFactory
                .Argument(defaultValueLookup.TryGetValue(parameter.Identifier.Text, out var defaultValue)
                    ? defaultValue
                    : fallbackDefaultValue)
                .WithNameColon(SyntaxFactory.NameColon(parameter.Identifier.ValueText));

            var arguments = parameterList.Parameters
                .Select(ConvertParameterToArgumentWithDefault)
                .ToSeparatedSyntaxList();

            return SyntaxFactory.ArrowExpressionClause(SyntaxFactory
                .ImplicitObjectCreationExpression()
                .WithArgumentList(SyntaxFactory.ArgumentList(arguments)));
        }

        private static bool IsDefaultWitherMethod(
            MemberDeclarationSyntax member,
            Dictionary<string, ParameterSyntax> defaultWitherMethodNameToParametersMapping)
        {
            return member.IsKind(SyntaxKind.MethodDeclaration)
                && defaultWitherMethodNameToParametersMapping.TryGetValue(
                    ((MethodDeclarationSyntax)member).Identifier.ValueText,
                    out var recordParameter)
                && ToParameterTypeAndName(recordParameter) == ToParameterTypeAndName(
                    ((MethodDeclarationSyntax)member).ParameterList.Parameters.First());
        }

        private static (string ParameterType, string ParameterName) ToParameterTypeAndName(ParameterSyntax node)
            => (node.Type.ToString(), node.Identifier.ValueText.ToCamelCase());
    }
}