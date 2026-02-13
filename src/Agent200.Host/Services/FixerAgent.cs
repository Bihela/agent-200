using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using System.Collections.ObjectModel;

namespace Agent200.Host.Services;

/// <summary>
/// Implementation of the Tier 3 Fixer Agent.
/// Uses the Microsoft Agent Framework to reason about remediation and execute GitHub tools.
/// </summary>
public class FixerAgent : IFixerAgent
{
    private readonly IChatClient _chatClient;
    private readonly IMcpService _mcpService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FixerAgent> _logger;

    public FixerAgent(
        IChatClient chatClient,
        IMcpService mcpService,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider)
    {
        _chatClient = chatClient;
        _mcpService = mcpService;
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger<FixerAgent>();
    }

    /// <inheritdoc />
    public AIAgent AsAgent()
    {
        var systemPrompt = @"You are a Senior SRE 'Fixer' Agent. Your goal is to apply automated remediation to identified infrastructure or code issues.

CRITICAL INSTRUCTIONS:
1. READ THE RCA: Carefully analyze the Root Cause Analysis provided.
2. USE GITHUB TOOLS: You have access to GitHub MCP tools. 
3. CREATE A BRANCH: Always create a new branch for your fix (e.g., 'fix/cpu-spike-remediation').
4. COMMIT THE FIX: Use 'create_or_update_file' to apply code or configuration changes.
5. OPEN A PULL REQUEST: Use 'create_pull_request' to propose the fix against the 'main' branch.
6. DO NOT MERGE: Your responsibility ends at creating the PR. A human must review and merge.

Available Context:
- Target Resource: asp-cpuspiker-free-central
- Resource Group: rg-opsweaver-hackathon
- GitHub Repository: Bihela/opsweaver-test-ground

Your output should be a summary of the PR you created.";

        return new ChatClientAgent(
            _chatClient,
            instructions: systemPrompt,
            name: "Fixer",
            loggerFactory: _loggerFactory,
            services: _serviceProvider
        );
    }

    /// <inheritdoc />
    /// <summary>
    /// Analyzes the provided Root Cause Analysis (RCA) report and attempts to autonomously fix the issue.
    /// This involves:
    /// 1. Creating a new git branch.
    /// 2. generating code or config changes.
    /// 3. Committing the changes.
    /// 4. Opening a Pull Request.
    /// </summary>
    /// <param name="rcaReport">The output from the Investigator Agent.</param>
    /// <returns>A summary of the actions taken (e.g., PR link).</returns>
    public async Task<string> RemediateAsync(string rcaReport)
    {
        _logger.LogInformation("🛠️ Fixer Agent: Starting remediation based on RCA...");

        var agent = AsAgent();
        var aiTools = await _mcpService.GetAIToolsAsync();
        var chatOptions = new ChatOptions { Tools = aiTools };

        var response = await agent.RunAsync(
            new ChatMessage(ChatRole.User, $"Please remediate the following RCA:\n\n{rcaReport}"),
            null,
            new ChatClientAgentRunOptions { ChatOptions = chatOptions }
        );

        return response.Messages.LastOrDefault()?.Text ?? "No remediation summary provided.";
    }
}
