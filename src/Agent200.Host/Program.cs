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
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;

// --------------------------------------------------------------------------------
// Agent 200: Autonomous SRE Host (Entry Point)
// --------------------------------------------------------------------------------
var builder = Host.CreateApplicationBuilder(args);

// Ensure User Secrets are loaded
builder.Configuration.AddUserSecrets<Program>();

// 1. Configure Services
builder.Services.AddSingleton<IMcpService, McpService>();
builder.Services.AddSingleton<IHealthEvaluator, HealthEvaluator>();
builder.Services.AddSingleton<IInvestigatorAgent, InvestigatorAgent>();
builder.Services.AddSingleton<IFixerAgent, FixerAgent>();
builder.Services.AddHostedService<WatchdogService>();

// --------------------------------------------------------------------------------
// 0. OpenTelemetry Configuration
// --------------------------------------------------------------------------------
var otelConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Agent200"))
    .WithTracing(tracing =>
    {
        tracing.AddSource("Agent200.AI");
        tracing.AddSource("Microsoft.Extensions.AI");
        tracing.AddHttpClientInstrumentation();
        
        if (!string.IsNullOrEmpty(otelConnectionString))
        {
            tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = otelConnectionString);
        }
    });

// --------------------------------------------------------------------------------
// 1. Dependency Injection Configuration
// --------------------------------------------------------------------------------
builder.Services.AddSingleton<IChatClient>(sp => {
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    
    var endpoint = configuration["AzureOpenAI:Endpoint"];
    var key = configuration["AzureOpenAI:Key"];
    var deployment = configuration["AzureOpenAI:Deployment"] ?? "gpt-4o-mini";

    if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
    {
        throw new InvalidOperationException("Missing Configuration (AzureOpenAI).");
    }

    var client = new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(key))
       .GetChatClient(deployment)
       .AsIChatClient();

    var sensitiveTools = new[] { "create_branch", "create_or_update_file", "create_pull_request", "push_files" };

    // We use AsBuilder() to wrap the core client with our HITL governance logic and Telemetry.
    return client.AsBuilder()
        .UseOpenTelemetry(sourceName: "Agent200.AI") // Enabled with explicit source name
        .Use(inner => new HumanInTheLoopChatClient(inner, sensitiveTools, loggerFactory.CreateLogger<HumanInTheLoopChatClient>()))
        .Build();
});

var host = builder.Build();

var config = host.Services.GetRequiredService<IConfiguration>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("[Host] Agent 200 Host started.");

// --------------------------------------------------------------------------------
// 3. Setup Interactive Agent capability
await host.StartAsync();

// --------------------------------------------------------------------------------
// 2. Interactive Agent Capability
// --------------------------------------------------------------------------------
// After the host starts and the background Watchdog is running, we enter an 
// interactive loop that allows a human to chat directly with the "Investigator" 
// capabilities of Agent 200.
// --------------------------------------------------------------------------------
Console.WriteLine("\n[Host] Agent 200 Host Started.");
Console.WriteLine("[Host] Watchdog is monitoring in the background.");
Console.WriteLine("[Host] You can still talk to the agent below.\n");

var subscriptionId = config["Azure:SubscriptionId"];
var tenantId = config["Azure:TenantId"];

if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(tenantId))
{
    Console.WriteLine("[Host] Azure:SubscriptionId or Azure:TenantId is missing in configuration.");
    return;
}

Microsoft.Extensions.AI.IChatClient agentClient = null;
List<Microsoft.Extensions.AI.AITool> aiTools = new List<Microsoft.Extensions.AI.AITool>();

try 
{
    var mcpService = host.Services.GetRequiredService<IMcpService>();
    var chatClient = host.Services.GetRequiredService<IChatClient>();
    var azureClient = await mcpService.GetAzureClientAsync(subscriptionId!, tenantId!);

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

    var isContainer = config["DOTNET_RUNNING_IN_CONTAINER"] == "true";
    if (isContainer)
    {
        Console.WriteLine("[Host] Container mode detected. Standing by...");
        await host.WaitForShutdownAsync();
        return;
    }

    while (true)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("USER: ");
        Console.ResetColor();
        var input = Console.ReadLine();
        if (string.IsNullOrEmpty(input) || input == "exit") break;

        try 
        {
            var response = await agentClient!.GetResponseAsync(
                new List<ChatMessage> {
                    new ChatMessage(ChatRole.System, systemPrompt),
                    new ChatMessage(ChatRole.User, input!)
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
