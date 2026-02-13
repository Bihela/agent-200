using Xunit;
using Agent200.Host;
using System.Runtime.InteropServices;

namespace Agent200.Tests;

public class McpServiceTests
{
    [Fact]
    public void CreateGitHubClientTransportOptions_ConfiguresCorrectlyForPlatform()
    {
        // Arrange
        var service = new McpService();
        var token = "test_token";

        // Act
        var options = service.CreateGitHubClientTransportOptions(token);

        // Assert
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal("npx.cmd", options.Command);
        }
        else
        {
            Assert.Equal("npx", options.Command);
        }

        Assert.Contains("-y", options.Arguments!);
        Assert.Contains("@modelcontextprotocol/server-github", options.Arguments!);
        Assert.NotNull(options.EnvironmentVariables);
        Assert.True(options.EnvironmentVariables.ContainsKey("GITHUB_PERSONAL_ACCESS_TOKEN"));
        Assert.Equal(token, options.EnvironmentVariables["GITHUB_PERSONAL_ACCESS_TOKEN"]);
    }
}
