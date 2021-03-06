using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Vinyl;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ConvertToRecordBasedBuilderAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "VINYL0001";
    private const string _category = "Simplification";
    private const string _title = "Class-based builder can be converted to record-based builder";
    private const string _messageFormat = "Class-based builder '{0}' can be converted to record-based builder";
    private const string _description = "Class-based builder should be using records instead.";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        _title,
        _messageFormat,
        _category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: _description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, ImmutableArray.Create(SyntaxKind.ClassDeclaration));
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classdeclaration = (ClassDeclarationSyntax)context.Node;

        if (classdeclaration.Identifier.ValueText.EndsWith("Builder", StringComparison.InvariantCulture)
            && AllFieldsHaveFieldNamingConvention(classdeclaration)
            && IsImmutableFluentBuilder(classdeclaration))
        {
            var location = Location.Create(
                context.Node.SyntaxTree,
                TextSpan.FromBounds(classdeclaration.Keyword.Span.Start, classdeclaration.Identifier.Span.End));

            var diagnostic = Diagnostic.Create(_rule, location, classdeclaration.Identifier.ValueText);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool AllFieldsHaveFieldNamingConvention(ClassDeclarationSyntax classdeclaration)
    {
        static bool HasFieldNamingConvention(string fieldName)
        {
            return fieldName.Length > 1
                && fieldName.StartsWith("_", StringComparison.InvariantCulture)
                && char.IsLower(fieldName[1]);
        }

        var fields = classdeclaration
            .Members
            .Where(x => x.IsKind(SyntaxKind.FieldDeclaration))
            .Cast<FieldDeclarationSyntax>();

        return fields.All(x => HasFieldNamingConvention(x.Declaration.Variables.Single().Identifier.Text));
    }

    private static bool IsImmutableFluentBuilder(ClassDeclarationSyntax classDeclaration)
    {
        return !classDeclaration.Members
            .Any(member =>
                member is MethodDeclarationSyntax method
                    && method.Identifier.ValueText.StartsWith("With", StringComparison.InvariantCulture)
                    && method.Body?.Statements.Any(x =>
                        x is ReturnStatementSyntax @return
                            && @return.Expression.IsKind(SyntaxKind.ThisExpression)) == true);
    }
}