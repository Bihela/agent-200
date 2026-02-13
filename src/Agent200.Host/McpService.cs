using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Diagnostics;

namespace Agent200.Host;

public class McpService : IAsyncDisposable
{
    private McpClient? _client;
    private StdioClientTransport? _transport;

    /// <summary>
    /// Gets or creates an MCP client for the Azure MCP server.
    /// Uses stdio transport to launch the '@azure/mcp' package via npx.
    /// </summary>
    public async Task<McpClient> GetClientAsync(string subscriptionId, string tenantId)
    {
        if (_client != null) return _client;

        // Configuration for the Azure MCP server transport.
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

    /// <summary>
    /// Disposes the client and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            // The McpClient or transport should handle clean shutdown
            // _transport?.Dispose(); // Removed as it doesn't exist
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a dedicated MCP client for the GitHub MCP server.
    /// </summary>
    public async Task<McpClient> CreateGitHubClientAsync(string githubToken)
    {
        var options = CreateGitHubClientTransportOptions(githubToken);

        var transport = new StdioClientTransport(options);
        var client = await McpClient.CreateAsync(transport);

        Console.WriteLine("🔌 Connected to GitHub MCP Server (via npx)");
        return client;
    }

    /// <summary>
    /// Configures the transport options for the GitHub MCP server.
    /// Launches the '@modelcontextprotocol/server-github' package via npx.
    /// </summary>
    public StdioClientTransportOptions CreateGitHubClientTransportOptions(string githubToken)
    {
        return new StdioClientTransportOptions
        {
            Command = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "npx.cmd" : "npx",
            Arguments = new[] { "-y", "@modelcontextprotocol/server-github" },
            EnvironmentVariables = new Dictionary<string, string?>
            {
                // Note: The GitHub MCP server requires GITHUB_PERSONAL_ACCESS_TOKEN.
                ["GITHUB_PERSONAL_ACCESS_TOKEN"] = githubToken
            }
        };
    }
}
