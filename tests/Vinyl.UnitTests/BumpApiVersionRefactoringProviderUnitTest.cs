using System;
using System.Threading.Tasks;

using Xunit;

using VerifyCS = Vinyl.UnitTests.CSharpCodeRefactoringVerifier<Vinyl.BumpApiVersionRefactoringProvider>;

namespace Vinyl
{
    public class BumpApiVersionRefactoringProviderUnitTest
    {
        [Fact]
        public async Task MyTestMethod()
        {
            var source = @"
using System;

[|[ApiVersion(""1.0"")]|]
public class Yolo
{
    public void Get()
    {
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class ApiVersionAttribute : Attribute
{
    public ApiVersionAttribute(string version)
    {
    }
}";

            var fixedSource = @"
using System;

[ApiVersion(""1.0"")]
[ApiVersion(""2.0"")]
public class Yolo
{
    public void Get()
    {
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class ApiVersionAttribute : Attribute
{
    public ApiVersionAttribute(string version)
    {
    }
}";

            await VerifyCS.VerifyRefactoringAsync(source, fixedSource);
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class ApiVersionAttribute : System.Attribute
    {
        public ApiVersionAttribute(string version)
        {
        }
    }


}