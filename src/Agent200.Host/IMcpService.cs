using ModelContextProtocol.Client;

namespace Agent200.Host;

/// <summary>
/// Service responsible for managing multiple persistent MCP clients (e.g., Azure, GitHub).
/// This interface enables mocking of the MCP infrastructure in unit tests.
/// </summary>
public interface IMcpService : IAsyncDisposable
{
    /// <summary>
    /// Gets or creates a persistent MCP client for Azure.
    /// </summary>
    /// <param name="subscriptionId">The Azure subscription ID.</param>
    /// <param name="tenantId">The Azure tenant ID.</param>
    /// <returns>An instance of IMcpClient connected to the Azure MCP server.</returns>
    Task<IMcpClient> GetAzureClientAsync(string subscriptionId, string tenantId);

    /// <summary>
    /// Gets or creates a persistent MCP client for GitHub.
    /// </summary>
    /// <param name="githubToken">The GitHub Personal Access Token.</param>
    /// <returns>An instance of IMcpClient connected to the GitHub MCP server.</returns>
    Task<IMcpClient> GetGitHubClientAsync(string githubToken);

    /// <summary>
    /// Returns all currently active and connected MCP clients.
    /// </summary>
    IEnumerable<IMcpClient> GetActiveClients();
}
