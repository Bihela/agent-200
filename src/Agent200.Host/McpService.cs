using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Diagnostics;

namespace Agent200.Host;

public class McpService
{
    public async Task<McpClient> CreateAzureClientAsync()
    {
        // This launches the Azure MCP server as a background process
        var options = new StdioClientTransportOptions
        {
            Command = "npx", 
            Arguments = new[] { "-y", "@azure/mcp", "server", "start" },
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["AZURE_SUBSCRIPTION_ID"] = "57eaaae6-c0cf-49b9-b983-8175c001de92",
                ["AZURE_TENANT_ID"] = "c1bf6a72-079d-4859-a0e4-630a4c416f80"
            }
        };

        var transport = new StdioClientTransport(options);
        var client = await McpClient.CreateAsync(transport);

        Console.WriteLine("🔌 Connected to Azure MCP Server");
        return client;
    }
}
