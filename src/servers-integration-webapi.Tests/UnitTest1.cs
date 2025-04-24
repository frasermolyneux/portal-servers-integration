namespace servers_integration_webapi.Tests;

public class Tests
{
    // No need for [SetUp] attribute in XUnit
    public void Setup()
    {
    }

    [Fact] // Using XUnit's [Fact] instead of NUnit's [Test]
    public void Test1()
    {
        // Using XUnit's Assert
        Assert.True(true);
    }
}