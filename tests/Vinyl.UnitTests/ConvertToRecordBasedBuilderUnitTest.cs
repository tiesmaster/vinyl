using VerifyCS = Vinyl.UnitTests.CSharpCodeFixVerifier<
    Vinyl.ConvertToRecordBasedBuilderAnalyzer,
    Vinyl.ConvertToRecordBasedBuilderCodeFixProvider>;

namespace Vinyl.Test;

public class ConvertToRecordBasedBuilderUnitTest
{
    [Fact]
    public async Task NoCodeNoDiagnostic()
    {
        const string test = "";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task GivenClassBasedBuilder_WhenAnalysing_ThenReportsTheLegacyBuilder()
    {
        const string test = @"
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

        var expected = VerifyCS.Diagnostic("VINYL0001").WithLocation(0).WithArguments("BookBuilder");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task GivenClassBasedBuilderAndFieldsAlreadyCamelCase_WhenAnalysing_ThenDoesNotReportsTheLegacyBuilder()
    {
        const string test = @"
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
        const string test = @"
namespace TestProject
{
    public {|#0:class BookBuilder|}
    {
        private readonly int _id;
        private readonly string _title;

        public BookBuilder(int id, string title)
        {
            _id = id;
            _title = title;
        }

        public BookBuilder()
        {
            _id = default;
            _title = string.Empty;
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

        const string fixtest = @"
namespace TestProject
{
    public record BookBuilder(int Id, string Title)
    {
        public static BookBuilder Default => new(Id: default, Title: string.Empty);

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

        var expected = VerifyCS.Diagnostic("VINYL0001").WithLocation(0).WithArguments("BookBuilder");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [Fact]
    public async Task GivenClassBasedBuilderWithoutClearlyDefaultSettingCtor_WhenFixing_ThenSimplyAddsDefaultCtor()
    {
        const string test = @"
namespace TestProject
{
    public {|#0:class BookBuilder|}
    {
        private readonly int _id;
        private readonly string _title;

        public BookBuilder(int id)
        {
            _id = id;
            _title = string.Empty;
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

        const string fixtest = @"
namespace TestProject
{
    public record BookBuilder(int Id, string Title)
    {
        public static BookBuilder Default => new(Id: default, Title: string.Empty);

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

        var expected = VerifyCS.Diagnostic("VINYL0001").WithLocation(0).WithArguments("BookBuilder");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [Fact]
    public async Task GivenClassBasedBuilderNonImmutable_WhenAnalysing_ThenDoesNotReport()
    {
        const string test = @"
namespace TestProject
{
    public {|#0:class BookBuilder|}
    {
        private readonly BookDto _dto;

        public BookBuilder()
        {
            _dto = new BookDto
            {
                Id = 0,
                Title = string.Empty
            };
        }

        public BookBuilder WithId(int id)
        {
            _dto.Id = id;
            return this;
        }

        public BookBuilder WithTitle(string title)
        {
            _dto.Title = title;
            return this;
        }

        public BookDto Build() => _dto;
        public static implicit operator BookDto(BookBuilder builder) => builder.Build();
    }

    public class BookDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task GivenClassBasedBuilderWithBuildMethodOnMultipleLines_WhenFixing_ThenMaintainsIndentation()
    {
        const string test = @"
namespace TestProject
{
    public {|#0:class BookBuilder|}
    {
        private readonly int _id;
        private readonly string _title;

        public BookBuilder(int id, string title)
        {
            _id = id;
            _title = title;
        }

        public BookBuilder()
        {
            _id = default;
            _title = string.Empty;
        }

        public BookBuilder WithId(int id) => new BookBuilder(id, _title);
        public BookBuilder WithTitle(string title) => new BookBuilder(_id, title);

        public Book Build() => new Book(
            _id,
            _title);

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

        const string fixtest = @"
namespace TestProject
{
    public record BookBuilder(int Id, string Title)
    {
        public static BookBuilder Default => new(Id: default, Title: string.Empty);

        public BookBuilder WithId(int id) => this with { Id = id };
        public BookBuilder WithTitle(string title) => this with { Title = title };

        public Book Build() => new(
            Id,
            Title);

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

        var expected = VerifyCS.Diagnostic("VINYL0001").WithLocation(0).WithArguments("BookBuilder");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }
}