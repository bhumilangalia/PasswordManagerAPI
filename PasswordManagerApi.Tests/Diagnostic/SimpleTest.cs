using Xunit;

namespace PasswordManagerApi.Tests.Diagnostic;

public class SimpleTest
{
    [Fact]
    public void SimplestTest_ShouldPass()
    {
        // This is the simplest possible test - no dependencies
        Assert.True(true);
    }

    [Fact]
    public void BasicMath_ShouldWork()
    {
        // Another simple test
        var result = 2 + 2;
        Assert.Equal(4, result);
    }
}
