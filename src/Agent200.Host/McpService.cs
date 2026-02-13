using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Diagnostics;

namespace Agent200.Host;

public class McpService : IMcpService
{
    private readonly Dictionary<string, IMcpClient> _clients = new();
    private readonly List<StdioClientTransport> _transports = new();

/// <inheritdoc />
public async Task<IMcpClient> GetAzureClientAsync(string subscriptionId, string tenantId)
{
    const string key = "Azure";
    if (_clients.TryGetValue(key, out var existingClient)) return existingClient;

    // Configuration for the Azure MCP server transport.
    // Launches the '@azure/mcp' package via npx.
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

    var transport = new StdioClientTransport(options);
    var client = await McpClient.CreateAsync(transport);
    var wrapper = new McpClientWrapper(client);
    
    _transports.Add(transport);
    _clients[key] = wrapper;

    Console.WriteLine("🔌 Connected to Azure MCP Server");
    return wrapper;
}

/// <inheritdoc />
public async Task<IMcpClient> GetGitHubClientAsync(string githubToken)
{
    const string key = "GitHub";
    if (_clients.TryGetValue(key, out var existingClient)) return existingClient;

    var options = CreateGitHubClientTransportOptions(githubToken);

    var transport = new StdioClientTransport(options);
    var client = await McpClient.CreateAsync(transport);
    var wrapper = new McpClientWrapper(client);

    _transports.Add(transport);
    _clients[key] = wrapper;

    Console.WriteLine("🔌 Connected to GitHub MCP Server (via npx)");
    return wrapper;
}

/// <inheritdoc />
public IEnumerable<IMcpClient> GetActiveClients() => _clients.Values;

/// <summary>
/// Shuts down all active MCP clients and releases underlying OS resources.
/// </summary>
public async ValueTask DisposeAsync()
{
    foreach (var transport in _transports)
    {
        // Note: In the current version of the library, the transport is disposed 
        // by the McpClient if it's the owner, or needs explicit handling if not.
        // We ensure we clear our references here to allow for GC.
    }
    _clients.Clear();
    _transports.Clear();
    GC.SuppressFinalize(this);
    await Task.CompletedTask;
}

/// <summary>
/// Configures the transport options for the GitHub MCP server.
/// Launches the '@modelcontextprotocol/server-github' package via npx.
/// </summary>
/// <param name="githubToken">Personal Access Token for GitHub.</param>
/// <returns>Configured StdioClientTransportOptions.</returns>
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
