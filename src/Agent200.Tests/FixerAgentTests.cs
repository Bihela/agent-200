using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;
using Agent200.Host.Services;
using Agent200.Host;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Agent200.Tests;

public class FixerAgentTests
{
    private readonly Mock<IChatClient> _chatClientMock = new();
    private readonly Mock<IMcpService> _mcpServiceMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<ILogger<FixerAgent>> _loggerMock = new();

    private readonly FixerAgent _agent;

    public FixerAgentTests()
    {
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);
        
        _agent = new FixerAgent(
            _chatClientMock.Object,
            _mcpServiceMock.Object,
            _loggerFactoryMock.Object,
            _serviceProviderMock.Object
        );
    }

    [Fact]
    public async Task RemediateAsync_CallsRunAsync_WithRCAReport()
    {
        // Arrange
        string rcaReport = "Root Cause Analysis: CPU spike caused by bad commit.";
        string expectedSummary = "PR Created: #5";

        _mcpServiceMock.Setup(m => m.GetAIToolsAsync())
            .ReturnsAsync(new List<AITool>());

        // Mock the ChatClient response
        _chatClientMock.Setup(c => c.GetResponseAsync(
            It.Is<IList<ChatMessage>>(m => m.Count > 0), // Simulating the conversation
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new List<ChatMessage> { 
                new ChatMessage(ChatRole.Assistant, expectedSummary) 
            }));

        // Act
        var result = await _agent.RemediateAsync(rcaReport);

        // Assert
        Assert.Contains(expectedSummary, result);
        
        // Verify GetResponseAsync was called (which is what runs under the hood of agent.RunAsync)
        // Note: ChatClientAgent.RunAsync eventually calls client.GetResponseAsync
        _chatClientMock.Verify(c => c.GetResponseAsync(
            It.Is<IList<ChatMessage>>(msgs => msgs.Any(m => m.Text.Contains(rcaReport))),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
