// expect tests without Assert: 1
using Xunit;

public class NoAssert
{
    [Fact]
    public void ChecksNothing()
    {
        var value = 1 + 1;
    }

    [Fact]
    public void ChecksSomething()
    {
        Assert.Equal(2, 1 + 1);
    }

    [Theory]
    [InlineData(1)]
    public void UsesAHelper(int value)
    {
        AssertPositive(value);
    }

    private static void AssertPositive(int value) => Assert.True(value > 0);
}
