using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Diagnostics;

namespace Agent200.Host;

public class McpService : IAsyncDisposable
{
    private McpClient? _client;
    private StdioClientTransport? _transport;

    public async Task<McpClient> GetClientAsync(string subscriptionId, string tenantId)
    {
        if (_client != null) return _client;

        // This launches the Azure MCP server as a background process
        var options = new StdioClientTransportOptions
        {
            Command = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "npx.cmd" : "npx", 
            Arguments = new[] { "-y", "@azure/mcp", "server", "start" },
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["AZURE_SUBSCRIPTION_ID"] = subscriptionId,
                ["AZURE_TENANT_ID"] = tenantId
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

    public async Task<McpClient> CreateGitHubClientAsync(string githubToken)
    {
        var options = CreateGitHubClientTransportOptions(githubToken);

        var transport = new StdioClientTransport(options);
        var client = await McpClient.CreateAsync(transport);

        Console.WriteLine("🔌 Connected to GitHub MCP Server (via npx)");
        return client;
    }

    public StdioClientTransportOptions CreateGitHubClientTransportOptions(string githubToken)
    {
        return new StdioClientTransportOptions
        {
            Command = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "npx.cmd" : "npx",
            Arguments = new[] { "-y", "@modelcontextprotocol/server-github" },
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["GITHUB_PERSONAL_ACCESS_TOKEN"] = githubToken
            }
        };
    }
}
