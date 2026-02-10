using Agent200.Host.Services;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Agent200.Tests;

public class HealthEvaluatorTests
{
    private readonly HealthEvaluator _evaluator = new();

    [Fact]
    public void IsHealthy_ReturnsTrue_WhenResourceIsPresent()
    {
        // Arrange
        var toolResult = new CallToolResult
        {
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = "Found resource: rg-opsweaver-hackathon" }
            }
        };
        var targetResource = "rg-opsweaver-hackathon";

        // Act
        var result = _evaluator.IsHealthy(toolResult, targetResource);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsHealthy_ReturnsFalse_WhenResourceIsMissing()
    {
        // Arrange
        var toolResult = new CallToolResult
        {
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = "No resources found." }
            }
        };
        var targetResource = "rg-opsweaver-hackathon";

        // Act
        var result = _evaluator.IsHealthy(toolResult, targetResource);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsHealthy_ReturnsFalse_WhenResultIsNull()
    {
        // Arrange
        CallToolResult? toolResult = null;
        var targetResource = "rg-opsweaver-hackathon";

        // Act
        var result = _evaluator.IsHealthy(toolResult!, targetResource);

        // Assert
        Assert.False(result);
    }
}
