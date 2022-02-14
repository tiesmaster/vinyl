using VerifyCS = Vinyl.UnitTests.CSharpCodeRefactoringVerifier<
    Vinyl.ExpandEfCoreSeededDataRefactoring>;

namespace Vinyl;

public class ExpandEfCoreSeededDataRefactoringUnitTests
{
    [Fact]
    public async Task TestAsync()
    {
        ExpandEfCoreSeededDataRefactoring.NewGuidFactory = () => new Guid("5fd45b56-fed7-4490-8ff6-97e31616ccf9");
        var source = @"using System;

public class BookDbContext
{
    public void OnModelCreating(ModelBuilder builder)
    {
        [|builder.Entity<Book>().HasData(
            new { Id = new Guid(""aa0d97b3-89b8-4a4c-a743-11d9925854dc""), Name = ""Catch-22"", Author = ""Joseph Heller"" },
            new { Id = new Guid(""9afaf16c-6fd5-49ac-ae92-0b9b096649ea""), Name = ""Nineteen Eighty-Four"", Author = ""George Orwell"" });|]
    }
}

public class Book
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Author { get; set; }
}

public class ModelBuilder
{
    public EntityTypeBuilder<TEntity> Entity<TEntity>() => throw new NotImplementedException();
}

public class EntityTypeBuilder<TEntity>
{
    public void HasData<TEntity>(params TEntity[] data) => throw new NotImplementedException();
}";

        var fixedSource = @"using System;

public class BookDbContext
{
    public void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Book>().HasData(
            new { Id = new Guid(""aa0d97b3-89b8-4a4c-a743-11d9925854dc""), Name = ""Catch-22"", Author = ""Joseph Heller"" },
            new { Id = new Guid(""9afaf16c-6fd5-49ac-ae92-0b9b096649ea""), Name = ""Nineteen Eighty-Four"", Author = ""George Orwell"" },
            new { Id = new Guid(""5fd45b56-fed7-4490-8ff6-97e31616ccf9""), Name = """", Author = """" });
    }
}

public class Book
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Author { get; set; }
}

public class ModelBuilder
{
    public EntityTypeBuilder<TEntity> Entity<TEntity>() => throw new NotImplementedException();
}

public class EntityTypeBuilder<TEntity>
{
    public void HasData<TEntity>(params TEntity[] data) => throw new NotImplementedException();
}";

        await VerifyCS.VerifyRefactoringAsync(source, fixedSource);
    }
}