using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Agents.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Agent200.Host.Services;

/// <summary>
/// Tier 2 Agent responsible for Root Cause Analysis (RCA).
/// Awakens when a metric anomaly is detected by the Watchdog.
/// </summary>
public class InvestigatorAgent : IInvestigatorAgent
{
    // Developer Note: This agent uses the Microsoft Agent Framework (ChatClientAgent) 
    // to correlate events between different platforms (Azure and GitHub).
    private readonly ILogger<InvestigatorAgent> _logger;
    private readonly IChatClient _chatClient;
    private readonly IMcpService _mcpService;
    private readonly IConfiguration _config;

    public InvestigatorAgent(
        ILogger<InvestigatorAgent> logger,
        IChatClient chatClient,
        IMcpService mcpService,
        IConfiguration config)
    {
        _logger = logger;
        _chatClient = chatClient;
        _mcpService = mcpService;
        _config = config;
    }

    /// <inheritdoc />
    public AIAgent AsAgent()
    {
        var subscriptionId = _config["Azure:SubscriptionId"];
        var tenantId = _config["Azure:TenantId"];

        // Construct the system prompt with dynamic context (Subscription, Tenant, Repo).
        // This ensures the LLM knows exactly which environment to investigate without hallucinating resource IDs.

        var systemPrompt = $@"You are a Senior SRE Investigator. Your goal is to find the ROOT CAUSE of the detected anomaly.

STEPS TO FOLLOW:
1. Examine Azure resources in the 'rg-opsweaver-hackathon' group using the 'group_list' tool.
2. Search for metric anomalies using the 'monitor_metrics_query' tool to confirm the spike.
3. Check GitHub repository for recent commits using the 'list_commits' tool.
4. Output a clear, concise 'ROOT CAUSE ANALYSIS' report.

Available Tools:
- 'group_list': Lists all resources in a group.
- 'monitor_metrics_query': Fetches metrics (CPU/Memory).
- 'list_commits': Fetches recent code changes.

Azure Context:
- Subscription: {subscriptionId}
- Tenant: {tenantId}
- GitHub Repository: Bihela/opsweaver-test-ground

CRITICAL: Use ONLY the provided tools. Do not mention tools you don't have access to (like azd). Always pass 'subscription' and 'tenant' to Azure tools.";

        return new ChatClientAgent(
            _chatClient, 
            instructions: systemPrompt, 
            name: "Investigator",
            description: "Senior SRE Investigator for Root Cause Analysis");
    }

    /// <summary>
    /// Performs an investigation into a detected anomaly.
    /// Correlates Azure metrics with GitHub activity and code changes.
    /// </summary>
    public async Task<string> InvestigateAnomalyAsync(string anomalyDescription)
    {
        _logger.LogInformation("[Investigator] Agent awakening to investigate: {Anomaly}", anomalyDescription);

        var agent = AsAgent();
        var allTools = await _mcpService.GetAIToolsAsync();
        var chatOptions = new ChatOptions { Tools = allTools };
        var runOptions = new ChatClientAgentRunOptions(chatOptions);
        
        try 
        {
            var response = await agent.RunAsync(anomalyDescription, options: runOptions);
            _logger.LogInformation("[Investigator] Investigation complete.");
            return response.Text ?? "No root cause identified.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during investigation.");
            return $"Investigation failed: {ex.Message}";
        }
    }
}
