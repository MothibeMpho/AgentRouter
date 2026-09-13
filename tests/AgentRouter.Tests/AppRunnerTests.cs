using System.Reflection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgentRouter.Tests;

public class AppRunnerTests
{
    [Fact]
    public void Constructor_SetsBaseUrl_FromConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string?> {
            { "LmStudio:BaseUrl", "http://example.local:5000" }
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var runner = new AppRunner(config);

        var field = typeof(AppRunner).GetField("_baseUrl", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = field!.GetValue(runner) as string;

        Assert.Equal("http://example.local:5000", value);
    }

    [Fact]
    public void Constructor_SetsBaseUrl_ToDefault_WhenMissing()
    {
        IConfiguration config = new ConfigurationBuilder().Build();

        var runner = new AppRunner(config);

        var field = typeof(AppRunner).GetField("_baseUrl", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = field!.GetValue(runner) as string;

        Assert.Equal("http://127.0.0.1:1234", value);
    }
}
