using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using OpenAI;
using Azure.AI.OpenAI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using McpDotNet.Extensions.AI;
using Agent200.Host;
using Agent200.Host.Services;

var builder = Host.CreateApplicationBuilder(args);

// Ensure User Secrets are loaded
builder.Configuration.AddUserSecrets<Program>();

// 1. Configure Services
builder.Services.AddSingleton<IMcpService, McpService>();
builder.Services.AddSingleton<IHealthEvaluator, HealthEvaluator>();
builder.Services.AddSingleton<IInvestigatorAgent, InvestigatorAgent>();
builder.Services.AddHostedService<WatchdogService>();

// 2. Register AI Components
var config = builder.Configuration;
var endpointString = config["AzureOpenAI:Endpoint"];
var key = config["AzureOpenAI:Key"];
var deploymentName = config["AzureOpenAI:Deployment"] ?? "gpt-4o-mini";

if (string.IsNullOrEmpty(endpointString) || string.IsNullOrEmpty(key))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Error: Missing Configuration (AzureOpenAI).");
    return;
}

IChatClient chatClient = new AzureOpenAIClient(new Uri(endpointString!), new System.ClientModel.ApiKeyCredential(key!))
   .GetChatClient(deploymentName)
   .AsIChatClient();

builder.Services.AddSingleton(chatClient);

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 Agent 200 Host started.");

// 3. Setup Interactive Agent capability
await host.StartAsync();

Console.WriteLine("\n🚀 Agent 200 Host Started.");
Console.WriteLine("🐶 Watchdog is monitoring in the background.");
Console.WriteLine("💬 You can still talk to the agent below.\n");

var subscriptionId = config["Azure:SubscriptionId"];
var tenantId = config["Azure:TenantId"];

if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(tenantId))
{
    Console.WriteLine("⚠️ Azure:SubscriptionId or Azure:TenantId is missing in configuration.");
    return;
}

var mcpService = host.Services.GetRequiredService<IMcpService>();
var azureClient = await mcpService.GetAzureClientAsync(subscriptionId, tenantId);
var aiTools = new List<AITool>();

// Helper to map MCP tools to Semantic Kernel AITool
/// <summary>
/// Maps an MCP tool to a Microsoft.Extensions.AI AITool.
/// Uses the McpAIFunction wrapper to ensure the MCP tool's input schema is preserved.
/// </summary>
AITool MapToAITool(McpClientTool tool, IMcpClient client)
{
    // Create the base AI function with a delegate that handles parameter extraction and tool invocation.
    var aiFunc = AIFunctionFactory.Create(async (AIFunctionArguments args, System.Threading.CancellationToken ct) => 
    {
        var dict = args.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var result = await client.CallToolAsync(tool.Name, new ReadOnlyDictionary<string, object?>(dict), null, null, ct);
        
        // Collate and return the tool's text output.
        var outputs = result.Content.Select(c => c is TextContentBlock t ? t.Text : c.ToString());
        return string.Join("\n", outputs);
    }, tool.Name, tool.Description);

    // Wrap the base function in McpAIFunction to override the schema metadata with the official MCP schema.
    return new McpAIFunction(aiFunc, tool.ProtocolTool.InputSchema);
}

// 1. Add Azure Tools
var azureTools = await azureClient.ListToolsAsync();
foreach(var tool in azureTools) 
{
    aiTools.Add(MapToAITool(tool, azureClient));
}

// 2. Add GitHub Tools (if configured)
var githubToken = config["GitHub:Token"];
if (!string.IsNullOrEmpty(githubToken))
{
    try 
    {
        Console.WriteLine("🔌 Connecting to GitHub...");
        var githubClient = await mcpService.GetGitHubClientAsync(githubToken);
        var githubToolsResult = await githubClient.ListToolsAsync();
        foreach(var tool in githubToolsResult)
        {
             aiTools.Add(MapToAITool(tool, githubClient));
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Failed to connect to GitHub: {ex.Message}");
        Console.WriteLine($"[Stack Trace]: {ex.StackTrace}");
    }
}
else
{
    Console.WriteLine("⚠️ GitHub Token not found. Skipping GitHub tools.");
}

var agentClient = chatClient.AsBuilder()
   .UseFunctionInvocation()
   .Build();

var systemPrompt = $@"You are an Azure expert assistant. You MUST use the provided tools to fetch real data. 
CLIENT CONTEXT:
- Subscription ID: {subscriptionId}
- Tenant ID: {tenantId}

CRITICAL INSTRUCTIONS:
1. To list resource groups, use the 'group_list' tool.
2. YOU MUST PASS BOTH 'tenant' AND 'subscription' PARAMETERS TO EVERY TOOL CALL.
3. For GitHub, use the provided tools to inspect repositories and workflows.
4. Do not make up data.";

while (true)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("USER: ");
    Console.ResetColor();
    var input = Console.ReadLine();
    if (string.IsNullOrEmpty(input) || input == "exit") break;

    try 
    {
        var response = await agentClient.GetResponseAsync(
            new List<ChatMessage> {
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, input)
            }, 
            new ChatOptions { Tools = aiTools }
        );

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nAGENT: {response.Text}\n");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error]: {ex.Message}");
    }
}

await host.StopAsync();

/// <summary>
/// A wrapper for AIFunction that allows overriding the JsonSchema.
/// This is used to pass the correct schema from MCP tools to the AI model,
/// ensuring that required parameters (like 'query' or 'repo') are correctly inferred by the model.
/// </summary>
class McpAIFunction : DelegatingAIFunction
{
    public McpAIFunction(AIFunction innerFunction, System.Text.Json.JsonElement jsonSchema)
        : base(innerFunction)
    {
        JsonSchema = jsonSchema;
    }

    public override System.Text.Json.JsonElement JsonSchema { get; }
}
