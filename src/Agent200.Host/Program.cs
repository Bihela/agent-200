using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using OpenAI;
using Azure.AI.OpenAI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Agent200.Host;
using Agent200.Host.Services;

var builder = Host.CreateApplicationBuilder(args);

// Ensure User Secrets are loaded
builder.Configuration.AddUserSecrets<Program>();

// 1. Configure Services
builder.Services.AddSingleton<McpService>();
builder.Services.AddSingleton<IHealthEvaluator, HealthEvaluator>();
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

IChatClient chatClient = new AzureOpenAIClient(new Uri(endpointString), new System.ClientModel.ApiKeyCredential(key))
   .GetChatClient(deploymentName)
   .AsIChatClient();

builder.Services.AddSingleton(chatClient);

var host = builder.Build();

// 3. Setup Interactive Agent capability
await host.StartAsync();

Console.WriteLine("\n🚀 Agent 200 Host Started.");
Console.WriteLine("🐶 Watchdog is monitoring in the background.");
Console.WriteLine("💬 You can still talk to the agent below.\n");

var mcpService = host.Services.GetRequiredService<McpService>();
var mcpClient = await mcpService.GetClientAsync();

var mcpTools = await mcpClient.ListToolsAsync();
var aiTools = new List<AITool>();

foreach (var tool in mcpTools)
{
    aiTools.Add(AIFunctionFactory.Create(async (AIFunctionArguments arguments, System.Threading.CancellationToken ct) => 
    {
        var dict = arguments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var result = await mcpClient.CallToolAsync(tool.Name, new ReadOnlyDictionary<string, object?>(dict), null, null, ct);
        
        var outputs = result.Content.Select(c => c is TextContentBlock t ? t.Text : c.ToString());
        return string.Join("\n", outputs);
    }, tool.Name, tool.Description));
}

var agentClient = chatClient.AsBuilder()
   .UseFunctionInvocation()
   .Build();

var systemPrompt = @"You are an Azure expert assistant. You MUST use the provided tools to fetch real data. 
CLIENT CONTEXT:
- Subscription ID: 57eaaae6-c0cf-49b9-b983-8175c001de92
- Tenant ID: c1bf6a72-079d-4859-a0e4-630a4c416f80

CRITICAL INSTRUCTIONS:
1. To list resource groups, use the 'group_list' tool.
2. YOU MUST PASS BOTH 'tenant' AND 'subscription' PARAMETERS TO EVERY TOOL CALL.
3. Do not make up data.";

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
