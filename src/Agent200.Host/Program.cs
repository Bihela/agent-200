using System.Collections.ObjectModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using OpenAI;
using Azure.AI.OpenAI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Agent200.Host;

// 1. Load Configuration (Secrets)
var config = new ConfigurationBuilder()
   .AddUserSecrets<Program>()
   .Build();

var endpointString = config["AzureOpenAI:Endpoint"];
var key = config["AzureOpenAI:Key"];
var deploymentName = config["AzureOpenAI:Deployment"] ?? "gpt-4o-mini";

if (string.IsNullOrEmpty(endpointString) || string.IsNullOrEmpty(key))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Error: Missing Configuration.");
    return;
}

var endpoint = new Uri(endpointString);

// 2. Initialize Client
IChatClient chatClient = new AzureOpenAIClient(endpoint, new System.ClientModel.ApiKeyCredential(key))
   .GetChatClient(deploymentName)
   .AsIChatClient();

// 3. Connect to Azure MCP
Console.WriteLine("🔌 Connecting to Azure...");
var mcpService = new McpService();
var mcpClient = await mcpService.CreateAzureClientAsync();

// 4. Get Tools from MCP and map them manually to AITool
var mcpTools = await mcpClient.ListToolsAsync();
var aiTools = new List<AITool>();

foreach (var tool in mcpTools)
{
    var aiTool = AIFunctionFactory.Create(async (AIFunctionArguments arguments, System.Threading.CancellationToken ct) => 
    {
        var dict = arguments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        IReadOnlyDictionary<string, object?> readOnlyArgs = new ReadOnlyDictionary<string, object?>(dict);
        
        try 
        {
            var result = await mcpClient.CallToolAsync(tool.Name, readOnlyArgs, null, null, ct);
            
            var textResults = result.Content
                .Select(c => c is TextContentBlock t ? t.Text : c.ToString())
                .ToList();
            
            return string.Join("\n", textResults);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }, tool.Name, tool.Description);
    
    aiTools.Add(aiTool);
}

Console.WriteLine($"🛠️ Loaded {aiTools.Count} tools from Azure.");

// 5. Run Agent with Tools
var agentClient = chatClient.AsBuilder()
   .UseFunctionInvocation()
   .Build();

var prompt = "List my Azure resource groups and tell me which one is the oldest.";
Console.WriteLine($"\nUSER: {prompt}\n");

try 
{
    var response = await agentClient.GetResponseAsync(
        new List<ChatMessage> {
            new ChatMessage(ChatRole.System, @"You are an Azure expert assistant. You MUST use the provided tools to fetch real data. 

CLIENT CONTEXT:
- Subscription ID: 57eaaae6-c0cf-49b9-b983-8175c001de92
- Tenant ID: c1bf6a72-079d-4859-a0e4-630a4c416f80

CRITICAL INSTRUCTIONS:
1. To list resource groups, use the 'group_list' tool.
2. YOU MUST PASS BOTH 'tenant' AND 'subscription' PARAMETERS TO EVERY TOOL CALL.
   - Example: group_list(tenant: 'c1bf6a72-079d-4859-a0e4-630a4c416f80', subscription: '57eaaae6-c0cf-49b9-b983-8175c001de92')
3. Do not make up data. If tools fail, explain the error."),
            new ChatMessage(ChatRole.User, prompt)
        }, 
        new ChatOptions { Tools = aiTools }
    );

    Console.WriteLine($"\nAGENT: {response.Text}");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n[Error]: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"[Inner Error]: {ex.InnerException.Message}");
    }
    Console.ResetColor();
}
