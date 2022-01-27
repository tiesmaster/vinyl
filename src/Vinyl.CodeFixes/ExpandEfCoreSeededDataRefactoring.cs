using System.Composition;

using Microsoft.CodeAnalysis.CodeRefactorings;

namespace Vinyl;

[Shared]
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(ExpandEfCoreSeededDataRefactoring))]
public class ExpandEfCoreSeededDataRefactoring : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        throw new NotImplementedException();
    }
}