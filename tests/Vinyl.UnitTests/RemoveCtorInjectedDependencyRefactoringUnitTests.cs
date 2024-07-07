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

    [Fact]
    public async Task GivenOnParameter_WhenEnclosingDeclarationIsNotCtor_ThenNoRefactoring()
    {
        const string source = """
            // namespace SomeNamespace;

            public class SomeClass
            {
                public void DoStuff(
                    ISomeService some[||]Service,
                    IAnotherService anotherService)
                {
                }
            }

            public interface ISomeService { }
            public interface IAnotherService { }
            """;

        await VerifyCS.VerifyRefactoringAsync(source, source);
    }

    [Fact]
    public async Task MostSimpleScenario()
    {
        const string source = """
            // namespace SomeNamespace;

            public class SomeClass
            {
                private readonly ISomeService _someService;
                private readonly IAnotherService _anotherService;

                public SomeClass(
                    ISomeService some[||]Service,
                    IAnotherService anotherService)
                {
                    _someService = someService;
                    _anotherService = anotherService;
                }
            }

            public interface ISomeService { }
            public interface IAnotherService { }
            """;

        const string fixedSource = """
            // namespace SomeNamespace;

            public class SomeClass
            {
                private readonly IAnotherService _anotherService;

                public SomeClass(
                    IAnotherService anotherService)
                {
                    _anotherService = anotherService;
                }
            }

            public interface ISomeService { }
            public interface IAnotherService { }
            """;

        await VerifyCS.VerifyRefactoringAsync(source, fixedSource);
    }
}