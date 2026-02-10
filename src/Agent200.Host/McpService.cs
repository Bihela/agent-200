using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Diagnostics;

namespace Agent200.Host;

public class McpService : IAsyncDisposable
{
    private McpClient? _client;
    private StdioClientTransport? _transport;

    public async Task<McpClient> GetClientAsync()
    {
        if (_client != null) return _client;

        // This launches the Azure MCP server as a background process
        var options = new StdioClientTransportOptions
        {
            Command = "npx", 
            Arguments = new[] { "-y", "@azure/mcp", "server", "start" },
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["AZURE_SUBSCRIPTION_ID"] = "57eaaae6-c0cf-49b9-b983-8175c001de92",
                ["AZURE_TENANT_ID"] = "c1bf6a72-079d-4859-a0e4-630a4c416f80"
            }
        };

        _transport = new StdioClientTransport(options);
        _client = await McpClient.CreateAsync(_transport);

        Console.WriteLine("🔌 Connected to Azure MCP Server");
        return _client;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            // The McpClient or transport should handle clean shutdown
            // _transport?.Dispose(); // Removed as it doesn't exist
        }
        await Task.CompletedTask;
    }
}
