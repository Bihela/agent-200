using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using Agent200.Host.Services;
using Agent200.Host;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.ObjectModel;

namespace Agent200.Tests;

/// <summary>
/// Unit tests for the <see cref="WatchdogService"/>.
/// These tests verify the critical handoff logic between Tier 1 (Watchdog) and Tier 2 (Investigator).
/// We use Moq to simulate unhealthy platform states without requiring real cloud connectivity.
/// </summary>
public class WatchdogServiceTests
{
    private readonly Mock<ILogger<WatchdogService>> _loggerMock = new();
    private readonly Mock<IConfiguration> _configMock = new();
    private readonly Mock<IMcpService> _mcpServiceMock = new();
    private readonly Mock<IHealthEvaluator> _healthEvaluatorMock = new();
    private readonly Mock<IInvestigatorAgent> _investigatorMock = new();
    private readonly Mock<IFixerAgent> _fixerMock = new();
    private readonly Mock<IMcpClient> _mcpClientMock = new();

    private readonly WatchdogService _service;

    public WatchdogServiceTests()
    {
        // Setup mock configuration
        _configMock.Setup(c => c["Azure:TenantId"]).Returns("test-tenant");
        _configMock.Setup(c => c["Azure:SubscriptionId"]).Returns("test-sub");

        // Mock the MCP client resolution
        _mcpServiceMock.Setup(s => s.GetAzureClientAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(_mcpClientMock.Object);

        // Setup a default mock response for CallToolAsync (e.g., successful monitor query)
        _mcpClientMock.Setup(c => c.CallToolAsync(It.IsAny<string>(), It.IsAny<ReadOnlyDictionary<string, object?>>(), It.IsAny<IProgress<ProgressNotificationValue>>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallToolResult { Content = new List<ContentBlock> { new TextContentBlock { Text = "mock metrics" } } });

        _service = new WatchdogService(
            _loggerMock.Object,
            _configMock.Object,
            _mcpServiceMock.Object,
            _healthEvaluatorMock.Object,
            _investigatorMock.Object,
            _fixerMock.Object);
    }

    [Fact]
    public async Task CheckMetricsAsync_TriggersInvestigator_WhenSystemIsUnhealthy()
    {
        // Arrange
        _healthEvaluatorMock.Setup(h => h.IsHealthy(It.IsAny<CallToolResult>(), It.IsAny<string>()))
            .Returns(false); // Unhealthy!
        
        _investigatorMock.Setup(i => i.InvestigateAnomalyAsync(It.IsAny<string>()))
            .ReturnsAsync("No root cause identified."); // Default RCA

        // Act
        await _service.CheckMetricsAsync(CancellationToken.None);

        // Assert
        _investigatorMock.Verify(i => i.InvestigateAnomalyAsync(It.Is<string>(s => s.Contains("CPU spike on"))), Times.Once);
    }
    
    [Fact]
    public async Task CheckMetricsAsync_TriggersFixer_WhenInvestigatorReturnsValidRCA()
    {
        // Arrange
        _healthEvaluatorMock.Setup(h => h.IsHealthy(It.IsAny<CallToolResult>(), It.IsAny<string>()))
            .Returns(false); // Unhealthy!

        _investigatorMock.Setup(i => i.InvestigateAnomalyAsync(It.IsAny<string>()))
            .ReturnsAsync("RCA: Database lock detected."); // Valid RCA

        // Act
        await _service.CheckMetricsAsync(CancellationToken.None);

        // Assert
        _investigatorMock.Verify(i => i.InvestigateAnomalyAsync(It.IsAny<string>()), Times.Once);
        _fixerMock.Verify(f => f.RemediateAsync(It.Is<string>(s => s.Contains("Database lock detected"))), Times.Once);
    }

    [Fact]
    public async Task CheckMetricsAsync_DoesNotTriggerFixer_WhenInvestigatorFails()
    {
        // Arrange
        _healthEvaluatorMock.Setup(h => h.IsHealthy(It.IsAny<CallToolResult>(), It.IsAny<string>()))
            .Returns(false); // Unhealthy!

        _investigatorMock.Setup(i => i.InvestigateAnomalyAsync(It.IsAny<string>()))
            .ReturnsAsync("No root cause identified"); // Invalid RCA

        // Act
        await _service.CheckMetricsAsync(CancellationToken.None);

        // Assert
        _investigatorMock.Verify(i => i.InvestigateAnomalyAsync(It.IsAny<string>()), Times.Once);
        _fixerMock.Verify(f => f.RemediateAsync(It.IsAny<string>()), Times.Never);
    }    

    [Fact]
    public async Task CheckMetricsAsync_DoesNotTriggerInvestigator_WhenSystemIsHealthy()
    {
        // Arrange
        _healthEvaluatorMock.Setup(h => h.IsHealthy(It.IsAny<CallToolResult>(), It.IsAny<string>()))
            .Returns(true); // Healthy

        // Act
        await _service.CheckMetricsAsync(CancellationToken.None);

        // Assert
        _investigatorMock.Verify(i => i.InvestigateAnomalyAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CheckMetricsAsync_LogsError_WhenMcpCallFails()
    {
        // Arrange
        _mcpClientMock.Setup(c => c.CallToolAsync(It.IsAny<string>(), It.IsAny<ReadOnlyDictionary<string, object?>>(), It.IsAny<IProgress<ProgressNotificationValue>>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("MCP error"));

        // Act
        await _service.CheckMetricsAsync(CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during Watchdog cycle") || v.ToString()!.Contains("Failed to call")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
