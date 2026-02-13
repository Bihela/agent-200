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
    private readonly McpService _mcpService;
    private readonly IConfiguration _config;

    public InvestigatorAgent(
        ILogger<InvestigatorAgent> logger,
        IChatClient chatClient,
        McpService mcpService,
        IConfiguration config)
    {
        _logger = logger;
        _chatClient = chatClient;
        _mcpService = mcpService;
        _config = config;
    }

    /// <summary>
    /// Performs an investigation into a detected anomaly.
    /// Correlates Azure metrics with GitHub activity and code changes.
    /// </summary>
    public async Task<string> InvestigateAnomalyAsync(string anomalyDescription)
    {
        _logger.LogInformation("🕵️ Investigator Agent awakening to investigate: {Anomaly}", anomalyDescription);

        // 1. Prepare Tools
        var allTools = await GetAllMcpToolsAsync();

        // 2. Configure the Agent
        var subscriptionId = _config["Azure:SubscriptionId"];
        var tenantId = _config["Azure:TenantId"];

        var systemPrompt = $@"You are a Senior SRE Investigator. Your goal is to find the ROOT CAUSE of the following anomaly:
{anomalyDescription}

STEPS TO FOLLOW:
1. Examine Azure resources in the 'rg-opsweaver-hackathon' group to see if any deployments or configuration changes occurred.
2. Search the GitHub repository for recent commits or failed Action workflows that align with the anomaly timing.
3. Fetch logs from failed workflows if possible.
4. Output a clear, concise 'ROOT CAUSE ANALYSIS' report.

Azure Context:
- Subscription: {subscriptionId}
- Tenant: {tenantId}

CRITICAL: Always pass 'subscription' and 'tenant' to Azure tools.";

        // 3. Create the Agent using Microsoft Agent Framework
        // Setting Name and Instructions via constructor as properties are read-only.
        var agent = new ChatClientAgent(
            _chatClient, 
            instructions: systemPrompt, 
            name: "Investigator",
            description: "Senior SRE Investigator for Root Cause Analysis");

        // 4. Run the Investigation
        // We use ChatClientAgentRunOptions to pass our toolset.
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

    /// <summary>
    /// Aggregates tools from all active MCP clients (e.g., Azure monitor, GitHub repo access).
    /// This allows the agent to reason across platform boundaries for RCA.
    /// </summary>
    private async Task<List<AITool>> GetAllMcpToolsAsync()
    {
        var aiTools = new List<AITool>();
        var clients = _mcpService.GetActiveClients();

        foreach (var client in clients)
        {
            // List all tools available on the current MCP client.
            var tools = await client.ListToolsAsync();
            foreach (var tool in tools)
            {
                // Map each MCP tool to a Microsoft.Extensions.AI tool for the agent to use.
                aiTools.Add(MapToAITool(tool, client));
            }
        }

        return aiTools;
    }

    private AITool MapToAITool(McpClientTool tool, IMcpClient client)
    {
        var aiFunc = AIFunctionFactory.Create(async (AIFunctionArguments args, System.Threading.CancellationToken ct) => 
        {
            var dict = args.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var result = await client.CallToolAsync(tool.Name, new ReadOnlyDictionary<string, object?>(dict), null, null, ct);
            
            var outputs = result.Content.Select(c => c is TextContentBlock t ? t.Text : c.ToString());
            return string.Join("\n", outputs);
        }, tool.Name, tool.Description);

        return new McpAIFunction(aiFunc, tool.ProtocolTool.InputSchema);
    }
}

/// <summary>
/// A wrapper for AIFunction that allows overriding the JsonSchema.
/// Internal to allow sharing logic with Program.cs if needed, but here for standalone capability.
/// </summary>
internal class McpAIFunction : DelegatingAIFunction
{
    public McpAIFunction(AIFunction innerFunction, JsonElement jsonSchema)
        : base(innerFunction)
    {
        JsonSchema = jsonSchema;
    }

    public override JsonElement JsonSchema { get; }
}
