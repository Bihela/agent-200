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

        var systemPrompt = $@"You are a Senior SRE Investigator. Your goal is to find the ROOT CAUSE of the following anomaly.

STEPS TO FOLLOW:
1. Examine Azure resources in the 'rg-opsweaver-hackathon' group to see if any deployments or configuration changes occurred.
2. Search the GitHub repository for recent commits or failed Action workflows that align with the anomaly timing.
3. Fetch logs from failed workflows if possible.
4. Output a clear, concise 'ROOT CAUSE ANALYSIS' report.

Azure Context:
- Subscription: {subscriptionId}
- Tenant: {tenantId}
- GitHub Repository: Bihela/opsweaver-test-ground

CRITICAL: Always pass 'subscription' and 'tenant' to Azure tools.";

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
        _logger.LogInformation("🕵️ Investigator Agent awakening to investigate: {Anomaly}", anomalyDescription);

        var agent = AsAgent();
        var allTools = await _mcpService.GetAIToolsAsync();
        var chatOptions = new ChatOptions { Tools = allTools };
        var runOptions = new ChatClientAgentRunOptions(chatOptions);
        
        try 
        {
            var response = await agent.RunAsync(anomalyDescription, options: runOptions);
            _logger.LogInformation("✅ Investigation complete.");
            return response.Text ?? "No root cause identified.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during investigation.");
            return $"Investigation failed: {ex.Message}";
        }
    }
}
