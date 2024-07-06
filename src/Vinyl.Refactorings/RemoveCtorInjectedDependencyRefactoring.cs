using System.Composition;

using Microsoft.CodeAnalysis.CodeRefactorings;

namespace Vinyl;

[Shared]
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RemoveCtorInjectedDependencyRefactoring))]
public class RemoveCtorInjectedDependencyRefactoring : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
    }
}