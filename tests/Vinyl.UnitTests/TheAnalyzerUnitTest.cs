using System.Threading.Tasks;

using Xunit;

using VerifyCS = Vinyl.UnitTests.CSharpCodeFixVerifier<
    Analyzer2.TheAnalyzerAnalyzer,
    Analyzer2.TheAnalyzerCodeFixProvider>;

// docs: https://github.com/dotnet/roslyn-sdk/blob/main/src/Microsoft.CodeAnalysis.Testing/README.md
// tut:  https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix
// awes: https://project-awesome.org/ironcev/awesome-roslyn
// src:  https://sourceroslyn.io/

namespace Analyzer2.Test
{
    public class TheAnalyzerUnitTest
    {
        [Fact]
        public async Task NoCodeNoDiagnostic()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task GivenClassBasedBuilder_WhenAnalysing_ThenReportsTheLegacyBuilder()
        {
            var test = @"
namespace TestProject
{
    public {|#0:class BookBuilder|}
    {
        private readonly int _id;
        private readonly string _title;

        public BookBuilder()
        {
            _id = default;
            _title = default;
        }

        public BookBuilder(int id, string title)
        {
            _id = id;
            _title = title;
        }

        public BookBuilder WithId(int id) => new BookBuilder(id, _title);
        public BookBuilder WithTitle(string title) => new BookBuilder(_id, title);

        public Book Build() => new Book(_id, _title);
        public static implicit operator Book(BookBuilder builder) => builder.Build();

        public BookBuilder WithDifferentId() => WithId(_id + 1);
    }

    public class Book
    {
        public Book(int id, string title)
        {
            Id = id;
            Title = title;
        }

        public int Id { get; }
        public string Title { get; }
    }
}";

            var expected = VerifyCS.Diagnostic("TheAnalyzer").WithLocation(0).WithArguments("BookBuilder");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task GivenClassBasedBuilderAndFieldsAlreadyCamelCase_WhenAnalysing_ThenDoesNotReportsTheLegacyBuilder()
        {
            var test = @"
namespace TestProject
{
    public class BookBuilder
    {
        private readonly int Id;
        private readonly string Title;

        public BookBuilder()
        {
            Id = default;
            Title = default;
        }

        public BookBuilder(int id, string title)
        {
            Id = id;
            Title = title;
        }

        public BookBuilder WithId(int id) => new BookBuilder(id, Title);
        public BookBuilder WithTitle(string title) => new BookBuilder(Id, title);

        public Book Build() => new Book(Id, Title);
        public static implicit operator Book(BookBuilder builder) => builder.Build();

        public BookBuilder WithDifferentId() => WithId(Id + 1);
    }

    public class Book
    {
        public Book(int id, string title)
        {
            Id = id;
            Title = title;
        }

        public int Id { get; }
        public string Title { get; }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task GivenClassBasedBuilder_WhenFixing_ThenDoesAllTheSteps()
        {
            var test = @"
namespace TestProject
{
    public {|#0:class BookBuilder|}
    {
        private readonly int _id;
        private readonly string _title;

        public BookBuilder()
        {
            _id = default;
            _title = default;
        }

        public BookBuilder(int id, string title)
        {
            _id = id;
            _title = title;
        }

        public BookBuilder WithId(int id) => new BookBuilder(id, _title);
        public BookBuilder WithTitle(string title) => new BookBuilder(_id, title);

        public Book Build() => new Book(_id, _title);
        public static implicit operator Book(BookBuilder builder) => builder.Build();

        public BookBuilder WithDifferentId() => WithId(_id + 1);
    }

    public class Book
    {
        public Book(int id, string title)
        {
            Id = id;
            Title = title;
        }

        public int Id { get; }
        public string Title { get; }
    }
}";

            var fixtest = @"
namespace TestProject
{
    public record BookBuilder(int Id, string Title)
    {
        public static BookBuilder Default => new(Id: default, Title: default);

        public BookBuilder WithId(int id) => this with { Id = id };
        public BookBuilder WithTitle(string title) => this with { Title = title };

        public Book Build() => new(Id, Title);
        public static implicit operator Book(BookBuilder builder) => builder.Build();

        public BookBuilder WithDifferentId() => WithId(Id + 1);
    }

    public class Book
    {
        public Book(int id, string title)
        {
            Id = id;
            Title = title;
        }

        public int Id { get; }
        public string Title { get; }
    }
}";

            var expected = VerifyCS.Diagnostic("TheAnalyzer").WithLocation(0).WithArguments("BookBuilder");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
        }
    }
}