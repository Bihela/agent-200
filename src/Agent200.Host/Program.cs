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
builder.Services.AddSingleton<IFixerAgent, FixerAgent>();
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

Microsoft.Extensions.AI.IChatClient agentClient = null;
List<Microsoft.Extensions.AI.AITool> aiTools = new List<Microsoft.Extensions.AI.AITool>();

try 
{
    var mcpService = host.Services.GetRequiredService<IMcpService>();
    var azureClient = await mcpService.GetAzureClientAsync(subscriptionId, tenantId);

    // Add GitHub client if token is present
    var githubToken = config["GitHub:Token"];
    if (!string.IsNullOrEmpty(githubToken))
    {
        await mcpService.GetGitHubClientAsync(githubToken);
    }

    // 4. Aggregate all tools from all clients
    aiTools = await mcpService.GetAIToolsAsync();

    agentClient = chatClient.AsBuilder()
       .UseFunctionInvocation()
       .Build();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[CRITICAL STARTUP ERROR]: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.ResetColor();
    return;
}

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
