using VerifyCS = Vinyl.UnitTests.CSharpCodeRefactoringVerifier<
    Vinyl.RemoveCtorInjectedDependencyRefactoring>;

namespace Vinyl;

public class RemoveCtorInjectedDependencyRefactoringUnitTests
{
    [Fact]
    public async Task EmptySource()
    {
        const string source = "[||]";
        await VerifyCS.VerifyRefactoringAsync(source, source);
    }
}