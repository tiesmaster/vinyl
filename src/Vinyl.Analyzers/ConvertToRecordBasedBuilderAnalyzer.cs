using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Vinyl
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ConvertToRecordBasedBuilderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TheAnalyzer";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly string Title = "Type name contains lowercase letters";
        private static readonly string MessageFormat = "Type name '{0}' contains lowercase letters";
        private static readonly string Description = "Type names should be all uppercase.";
        private const string Category = "Naming";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, ImmutableArray.Create(SyntaxKind.ClassDeclaration));

            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            //context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
        {
            var classdeclaration = (ClassDeclarationSyntax)context.Node;

            if (classdeclaration.Identifier.ValueText.EndsWith("Builder")
                && AllFieldsHaveFieldNamingConvention(classdeclaration))
            {
                var location = Location.Create(
                    context.Node.SyntaxTree,
                    TextSpan.FromBounds(classdeclaration.Keyword.Span.Start, classdeclaration.Identifier.Span.End));

                var diagnostic = Diagnostic.Create(Rule, location, classdeclaration.Identifier.ValueText);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool AllFieldsHaveFieldNamingConvention(ClassDeclarationSyntax classdeclaration)
        {
            bool HasFieldNamingConvention(string fieldName)
            {
                return fieldName.Length > 1 && fieldName.StartsWith("_") && char.IsLower(fieldName[1]);
            }

            var fields = classdeclaration
                .Members
                .Where(x => x.IsKind(SyntaxKind.FieldDeclaration))
                .Cast<FieldDeclarationSyntax>();

            return fields.All(x => HasFieldNamingConvention(x.Declaration.Variables.Single().Identifier.Text));
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Find just those named type symbols with names containing lowercase letters.
            if (namedTypeSymbol.Name.ToCharArray().Any(char.IsLower))
            {
                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}